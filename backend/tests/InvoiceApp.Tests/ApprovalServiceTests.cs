using InvoiceApp.Domain;
using Xunit;

namespace InvoiceApp.Tests;

public class ApprovalServiceTests
{
    private static Invoice InvoiceWith(string id, InvoiceStatus status, decimal amount = 100m, List<string>? approvers = null) =>
        new() { Id = id, CustomerName = "Test School", Amount = amount, Status = status, Approvers = approvers ?? new List<string>() };

    private static ApprovalService ServiceFor(params Invoice[] invoices) =>
        new(new FakeInvoiceRepository(invoices));

    [Fact]
    public void Submit_MovesDraftToPendingApproval()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Draft);

        var result = ServiceFor(invoice).Submit("INV-1");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
    }

    [Fact]
    public void Submit_RejectsInvoiceThatIsNotADraft()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.Approved));

        Assert.Throws<InvalidOperationException>(() => service.Submit("INV-1"));
    }

    [Fact]
    public void Approve_SetsStatusApproverAndTimestamp()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        var result = service.Approve("INV-1", "demo.approver");

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.approver", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
    }

    [Fact]
    public void Approve_RejectsInvoiceThatIsStillADraft()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.Draft));

        Assert.Throws<InvalidOperationException>(() => service.Approve("INV-1", "demo.approver"));
    }

    [Fact]
    public void Approve_ThrowsWhenInvoiceDoesNotExist()
    {
        var service = ServiceFor();

        Assert.Throws<KeyNotFoundException>(() => service.Approve("INV-404", "demo.approver"));
    }

    [Fact]
    public void Approve_AllowsManagerToApproveUpTo50k()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 50_000m));

        var result = service.Approve("INV-1", "manager.one");

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("manager.one", result.DecidedBy);
    }

    [Fact]
    public void Approve_RejectsManagerAbove50kWithoutChangingStatus()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 50_001m);
        var service = ServiceFor(invoice);

        var result = service.Approve("INV-1", "manager.one");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        Assert.Null(result.DecidedBy);
        Assert.Null(result.DecidedAt);
        Assert.Equal("Invoice amount £50,001.00 exceeds the Manager approval limit of £50,000.00. Only a Director may approve up to £500,000.00.", result.DecisionNote);
    }

    [Fact]
    public void Approve_AllowsDirectorToApproveUpTo500k()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 500_000m));

        var result = service.Approve("INV-1", "director.one");

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("director.one", result.DecidedBy);
    }

    [Fact]
    public void Approve_RejectsDirectorAbove500kWithoutChangingStatus()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 500_001m);
        var service = ServiceFor(invoice);

        var result = service.Approve("INV-1", "director.one");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        Assert.Null(result.DecidedBy);
        Assert.Null(result.DecidedAt);
        Assert.Equal("Invoice amount £500,001.00 exceeds the Director approval limit of £500,000.00. A single approver cannot approve amounts above that limit.", result.DecisionNote);
    }

    [Fact]
    public void Approve_RequiresTwoDistinctApprovers_WhenInvoiceExceeds500k()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 500_001m, new List<string> { "manager.one" });
        var service = ServiceFor(invoice);

        var result = service.Approve("INV-1", "director.two");

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("director.two", result.DecidedBy);
        Assert.Equal(2, result.Approvers.Count);
        Assert.Contains("manager.one", result.Approvers);
        Assert.Contains("director.two", result.Approvers);
    }

    [Fact]
    public void Approve_RejectsDuplicateApprover_WhenInvoiceExceeds500k()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 500_001m, new List<string> { "manager.one", "director.two" });
        var service = ServiceFor(invoice);

        var result = service.Approve("INV-1", "director.two");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        Assert.Null(result.DecidedBy);
        Assert.Null(result.DecidedAt);
        Assert.Equal("A user cannot approve the same invoice twice.", result.DecisionNote);
    }

    [Fact]
    public void Approve_RejectsNonDirectorSecondApprover_WhenInvoiceExceeds500k()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 500_001m, new List<string> { "manager.one" });
        var service = ServiceFor(invoice);

        var result = service.Approve("INV-1", "manager.two");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        Assert.Null(result.DecidedBy);
        Assert.Null(result.DecidedAt);
        Assert.Equal("Invoice amount £500,001.00 requires two distinct approvers, and the second approver must be a Director.", result.DecisionNote);
    }

    [Fact]
    public void Reject_RecordsReasonAndApprover()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        var result = service.Reject("INV-1", "demo.approver", "Duplicate invoice");

        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("Duplicate invoice", result.DecisionNote);
    }

    [Fact]
    public void Reject_RequiresAReason()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        Assert.Throws<ArgumentException>(() => service.Reject("INV-1", "demo.approver", "  "));
    }
}
