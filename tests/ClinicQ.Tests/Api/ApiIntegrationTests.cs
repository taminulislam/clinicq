using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClinicQ.Web.Api.Contracts;

namespace ClinicQ.Tests.Api;

/// <summary>
/// End-to-end tests over the real HTTP pipeline: JWT auth, versioned routes, validation, the appointment
/// state machine and billing, all against the SQLite fallback.
/// </summary>
public class ApiIntegrationTests : IClassFixture<ClinicQApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ClinicQApiFactory _factory;

    public ApiIntegrationTests(ClinicQApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_endpoint_is_anonymous_and_reports_healthy()
    {
        var response = await _factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Protected_endpoints_require_a_bearer_token()
    {
        var response = await _factory.CreateClient().GetAsync("/api/v1/patients");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_credentials_are_rejected()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/token", new TokenRequest { Username = "admin", Password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_seeded_user_can_exchange_credentials_for_a_token()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/token", new TokenRequest { Username = "drpatel", Password = "Passw0rd!" });
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(Json);

        Assert.NotNull(token);
        Assert.Equal("Bearer", token!.TokenType);
        Assert.Equal("Doctor", token.Role);
        Assert.Equal(3, token.AccessToken.Split('.').Length);   // header.payload.signature
        Assert.True(token.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Reference_data_seeded_at_startup_is_served()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var branches = await client.GetFromJsonAsync<List<BranchResponse>>("/api/v1/branches", Json);
        Assert.NotNull(branches);
        Assert.Equal(3, branches!.Count);
        Assert.Contains(branches, b => b.Code == "SPI");

        var doctors = await client.GetFromJsonAsync<List<DoctorResponse>>($"/api/v1/doctors?branchId={branches[0].Id}", Json);
        Assert.NotEmpty(doctors!);

        var fees = await client.GetFromJsonAsync<List<FeeScheduleItemResponse>>($"/api/v1/branches/{branches[0].Id}/fees", Json);
        Assert.Contains(fees!, f => f.ServiceCode == "CONSULT");
    }

    [Fact]
    public async Task Unknown_ids_produce_problem_details_404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/patients/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Resource not found", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Invalid_payloads_are_rejected_with_validation_errors()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/appointments", new CreateAppointmentRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("Reason", out _));
        Assert.True(errors.TryGetProperty("PatientId", out _));
    }

    [Fact]
    public async Task A_patient_can_be_created_and_read_back()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var create = await client.PostAsJsonAsync("/api/v1/patients", new CreatePatientRequest
        {
            FirstName = "Grace",
            LastName = "Hopper",
            DateOfBirth = new DateTime(1906, 12, 9),
            Gender = "Female",
            Email = "grace.hopper@example.com",
            Phone = "(217) 555-0199"
        });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<PatientResponse>(Json);
        Assert.StartsWith("MRN-", created!.Mrn);

        var fetched = await client.GetFromJsonAsync<PatientResponse>($"/api/v1/patients/{created.Id}", Json);
        Assert.Equal("Grace Hopper", fetched!.FullName);

        var search = await client.GetFromJsonAsync<List<PatientResponse>>("/api/v1/patients?search=hopper", Json);
        Assert.Contains(search!, p => p.Id == created.Id);
    }

    [Fact]
    public async Task Booking_walks_the_state_machine_and_rejects_illegal_moves()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var (branchId, doctorId, slot) = await FindFreeSlotAsync(client);
        var patients = await client.GetFromJsonAsync<List<PatientResponse>>("/api/v1/patients?limit=1", Json);

        var create = await client.PostAsJsonAsync("/api/v1/appointments", new CreateAppointmentRequest
        {
            PatientId = patients![0].Id,
            DoctorId = doctorId,
            BranchId = branchId,
            Start = slot,
            Reason = "Integration test visit"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var appointment = await create.Content.ReadFromJsonAsync<AppointmentResponse>(Json);
        Assert.Equal("Requested", appointment!.Status);
        Assert.Contains("Confirmed", appointment.AllowedTransitions);

        // Skipping check-in is a conflict, not a crash.
        var illegal = await client.PostAsync($"/api/v1/appointments/{appointment.Id}/start", null);
        Assert.Equal(HttpStatusCode.Conflict, illegal.StatusCode);

        foreach (var (step, expected) in new[] { ("confirm", "Confirmed"), ("check-in", "CheckedIn"), ("start", "InConsultation") })
        {
            var move = await client.PostAsync($"/api/v1/appointments/{appointment.Id}/{step}", null);
            move.EnsureSuccessStatusCode();
            Assert.Equal(expected, (await move.Content.ReadFromJsonAsync<AppointmentResponse>(Json))!.Status);
        }

        var complete = await client.PostAsJsonAsync($"/api/v1/appointments/{appointment.Id}/complete", new CompleteAppointmentRequest { Notes = "All clear" });
        complete.EnsureSuccessStatusCode();
        var completed = await complete.Content.ReadFromJsonAsync<AppointmentResponse>(Json);
        Assert.Equal("Completed", completed!.Status);
        Assert.Empty(completed.AllowedTransitions);
        Assert.NotNull(completed.WaitMinutes);

        // Billing the completed visit.
        var invoiceResponse = await client.PostAsJsonAsync("/api/v1/invoices", new GenerateInvoiceRequest
        {
            AppointmentId = appointment.Id,
            Services = { new ServiceLineDto { ServiceCode = "CONSULT", Quantity = 1 } }
        });
        Assert.Equal(HttpStatusCode.Created, invoiceResponse.StatusCode);
        var invoice = await invoiceResponse.Content.ReadFromJsonAsync<InvoiceResponse>(Json);
        Assert.Equal("Issued", invoice!.Status);
        Assert.True(invoice.Total > 0);

        var payment = await client.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id}/payments", new RecordPaymentRequest
        {
            Amount = invoice.Total,
            Method = "Card",
            Reference = "RCPT-TEST"
        });
        payment.EnsureSuccessStatusCode();
        Assert.Equal("Paid", (await payment.Content.ReadFromJsonAsync<InvoiceResponse>(Json))!.Status);

        // And the PDF renders.
        var pdf = await client.GetAsync($"/api/v1/invoices/{invoice.Id}/pdf");
        pdf.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        Assert.NotEmpty(await pdf.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Double_booking_the_same_slot_is_refused()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var (branchId, doctorId, slot) = await FindFreeSlotAsync(client);
        var patients = await client.GetFromJsonAsync<List<PatientResponse>>("/api/v1/patients?limit=1", Json);

        var request = new CreateAppointmentRequest
        {
            PatientId = patients![0].Id,
            DoctorId = doctorId,
            BranchId = branchId,
            Start = slot,
            Reason = "First booking"
        };

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/appointments", request)).StatusCode);

        request.Reason = "Second booking";
        var conflict = await client.PostAsJsonAsync("/api/v1/appointments", request);

        Assert.Equal(HttpStatusCode.BadRequest, conflict.StatusCode);
        var problem = await conflict.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("fully booked", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Role_restricted_endpoints_reject_other_roles()
    {
        var reception = await _factory.CreateAuthenticatedClientAsync("reception");

        var response = await reception.PostAsJsonAsync("/api/v1/billing/reconcile", new ReconcileRequest());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var billing = await _factory.CreateAuthenticatedClientAsync("billing");
        var allowed = await billing.PostAsJsonAsync("/api/v1/billing/reconcile", new ReconcileRequest());
        allowed.EnsureSuccessStatusCode();
        Assert.NotEmpty((await allowed.Content.ReadFromJsonAsync<List<ReconciliationRunResponse>>(Json))!);
    }

    [Fact]
    public async Task Dashboard_metrics_are_returned_for_a_date_range()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var metrics = await client.GetFromJsonAsync<JsonElement>("/api/v1/dashboard/metrics");

        Assert.True(metrics.TryGetProperty("doctorUtilization", out var utilization));
        Assert.True(utilization.GetArrayLength() > 0);
        Assert.True(metrics.TryGetProperty("revenue", out _));
        Assert.True(metrics.TryGetProperty("statusCounts", out _));
    }

    [Fact]
    public async Task Swagger_document_is_published()
    {
        var response = await _factory.CreateClient().GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();

        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        var paths = document.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/auth/token", out _));
        Assert.True(paths.TryGetProperty("/api/v1/appointments", out _));
    }

    /// <summary>Finds the next day with a free slot for the first doctor at the first branch.</summary>
    private static async Task<(int BranchId, int DoctorId, DateTime Slot)> FindFreeSlotAsync(HttpClient client)
    {
        var branches = await client.GetFromJsonAsync<List<BranchResponse>>("/api/v1/branches", Json);
        var branch = branches!.Single(b => b.Code == "SPI");
        var doctors = await client.GetFromJsonAsync<List<DoctorResponse>>($"/api/v1/doctors?branchId={branch.Id}", Json);
        var doctor = doctors![0];

        for (var offset = 1; offset <= 14; offset++)
        {
            var date = DateTime.Today.AddDays(offset).ToString("yyyy-MM-dd");
            var slots = await client.GetFromJsonAsync<List<SlotResponse>>($"/api/v1/branches/{branch.Id}/slots?doctorId={doctor.Id}&date={date}", Json);
            var free = slots!.FirstOrDefault(s => s.IsAvailable);
            if (free is not null)
            {
                return (branch.Id, doctor.Id, free.Start);
            }
        }

        throw new InvalidOperationException("No bookable slot found in the next two weeks.");
    }
}
