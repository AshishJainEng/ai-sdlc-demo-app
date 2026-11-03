using System.Globalization;

namespace InvoiceApp.Domain;

/// <summary>
/// Moves an invoice through its lifecycle: Draft -> PendingApproval -> Approved or Rejected.
/// One invoice at a time, decided as a whole by a single approver.
/// </summary>
public class ApprovalService
{
    private const decimal ManagerApprovalLimit = 50_000m;
    private const decimal DirectorApprovalLimit = 500_000m;
    private static readonly CultureInfo BritishPounds = CultureInfo.GetCultureInfo("en-GB");

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

        var rejectionReason = ValidateApproval(invoice, approver);
        if (rejectionReason is not null)
        {
            invoice.DecisionNote = rejectionReason;
            _repository.Update(invoice);
            return invoice;
        }

        invoice.Approvers ??= new List<string>();
        invoice.Approvers.Add(approver);

        if (invoice.Amount > DirectorApprovalLimit)
        {
            if (invoice.Approvers.Count >= 2)
            {
                invoice.Status = InvoiceStatus.Approved;
                invoice.DecidedBy = approver;
                invoice.DecidedAt = DateTime.UtcNow;
                invoice.DecisionNote = null;
            }
            else
            {
                invoice.Status = InvoiceStatus.PendingApproval;
                invoice.DecidedBy = null;
                invoice.DecidedAt = null;
                invoice.DecisionNote = null;
            }

            _repository.Update(invoice);
            return invoice;
        }

        invoice.Status = InvoiceStatus.Approved;
        invoice.DecidedBy = approver;
        invoice.DecidedAt = DateTime.UtcNow;
        invoice.DecisionNote = null;
        _repository.Update(invoice);
        return invoice;
    }

    public string? ValidateApproval(Invoice invoice, string approver)
    {
        if (invoice.Amount <= ManagerApprovalLimit)
        {
            return null;
        }

        if (invoice.Amount <= DirectorApprovalLimit)
        {
            return IsDirector(approver)
                ? null
                : $"Invoice amount {FormatCurrency(invoice.Amount)} exceeds the Manager approval limit of {FormatCurrency(ManagerApprovalLimit)}. Only a Director may approve up to {FormatCurrency(DirectorApprovalLimit)}.";
        }

        var existingApprovers = invoice.Approvers ?? new List<string>();

        if (existingApprovers.Contains(approver, StringComparer.OrdinalIgnoreCase))
        {
            return "A user cannot approve the same invoice twice.";
        }

        if (existingApprovers.Count == 0)
        {
            return IsDirector(approver)
                ? $"Invoice amount {FormatCurrency(invoice.Amount)} exceeds the Director approval limit of {FormatCurrency(DirectorApprovalLimit)}. A single approver cannot approve amounts above that limit."
                : null;
        }

        if (!IsDirector(approver))
        {
            return $"Invoice amount {FormatCurrency(invoice.Amount)} requires two distinct approvers, and the second approver must be a Director.";
        }

        return null;
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

    private static string FormatCurrency(decimal amount) => amount.ToString("C", BritishPounds);

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
