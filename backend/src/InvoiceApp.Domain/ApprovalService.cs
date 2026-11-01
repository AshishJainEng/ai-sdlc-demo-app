namespace InvoiceApp.Domain;

/// <summary>
/// Moves an invoice through its lifecycle: Draft -> PendingApproval -> Approved or Rejected.
/// One invoice at a time, decided as a whole by a single approver.
/// </summary>
public class ApprovalService
{
    private const decimal ManagerApprovalLimit = 50_000m;
    private const decimal DirectorApprovalLimit = 500_000m;

    private readonly IInvoiceRepository _repository;

    public ApprovalService(IInvoiceRepository repository)
    {
        _repository = repository;
    }

    public Invoice Submit(string invoiceId)
    {
        var invoice = Find(invoiceId);

        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Invoice {invoiceId} is {invoice.Status}; only drafts can be submitted.");
        }

        invoice.Status = InvoiceStatus.PendingApproval;
        _repository.Update(invoice);
        return invoice;
    }

    public Invoice Approve(string invoiceId, string approver)
    {
        var invoice = Find(invoiceId);
        EnsurePending(invoice);

        var rejectionReason = ValidateApprovalLimit(invoice, approver);
        if (rejectionReason is not null)
        {
            invoice.DecisionNote = rejectionReason;
            return invoice;
        }

        invoice.Status = InvoiceStatus.Approved;
        invoice.DecidedBy = approver;
        invoice.DecidedAt = DateTime.UtcNow;
        invoice.DecisionNote = null;
        invoice.Approvers.Add(approver);
        _repository.Update(invoice);
        return invoice;
    }

    public string? ValidateApprovalLimit(Invoice invoice, string approver)
    {
        if (invoice.Amount <= ManagerApprovalLimit)
        {
            return null;
        }

        if (invoice.Amount <= DirectorApprovalLimit)
        {
            if (IsDirector(approver))
            {
                return null;
            }

            return $"Invoice amount {invoice.Amount:C} exceeds the Manager approval limit of {ManagerApprovalLimit:C}. Only a Director may approve up to {DirectorApprovalLimit:C}.";
        }

        if (IsDirector(approver))
        {
            return $"Invoice amount {invoice.Amount:C} exceeds the Director approval limit of {DirectorApprovalLimit:C}. A single approver cannot approve amounts above that limit.";
        }

        return $"Invoice amount {invoice.Amount:C} exceeds the Manager approval limit of {ManagerApprovalLimit:C}. Only a Director may approve up to {DirectorApprovalLimit:C}.";
    }

    public Invoice Reject(string invoiceId, string approver, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to reject an invoice.", nameof(reason));
        }

        var invoice = Find(invoiceId);
        EnsurePending(invoice);

        invoice.Status = InvoiceStatus.Rejected;
        invoice.DecidedBy = approver;
        invoice.DecidedAt = DateTime.UtcNow;
        invoice.DecisionNote = reason;
        _repository.Update(invoice);
        return invoice;
    }

    private static bool IsDirector(string approver) =>
        approver.Contains("director", StringComparison.OrdinalIgnoreCase);

    private Invoice Find(string invoiceId) =>
        _repository.GetById(invoiceId)
        ?? throw new KeyNotFoundException($"Invoice {invoiceId} was not found.");

    private static void EnsurePending(Invoice invoice)
    {
        if (invoice.Status != InvoiceStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                $"Invoice {invoice.Id} is {invoice.Status}; only pending invoices can be decided.");
        }
    }
}
