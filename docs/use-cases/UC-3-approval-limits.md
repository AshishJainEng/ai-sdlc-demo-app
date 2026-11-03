# UC-3: Approval limits

## Background

Finance and audit teams rely on clear approval authority limits to control risk and ensure larger invoices are reviewed by appropriate personnel. The current approval flow is too permissive for high-value invoices and needs explicit limits tied to role and approval count.

## The change

The approval process will enforce role-based authority thresholds and multiple-approver requirements for large invoices. A Manager can approve up to 50,000. A Director can approve up to 500,000. Any invoice above 500,000 requires two distinct approvers, and the second approver must be a Director. The same person cannot serve as both approvers on the same invoice. If an approver tries to approve under invalid conditions, the system returns a clear error message and does not change the invoice's status.

## What the business asked for

- Manager approval limit is 50,000.
- Director approval limit is 500,000.
- Invoices over 500,000 require two distinct approvers.
- The second approver on large invoices must be a Director.
- The same user cannot act as both approvers.
- Rejections must return a reason string and leave the invoice status unchanged.
- Existing approval behavior remains valid unless the invoice violates the new policy.
- Validation happens in the domain approval rules, not in the controller layer.

## Where it touches the code

The new rules sit in the domain approval logic, primarily:
- `ApprovalService`
- related approvals endpoints in `ApprovalsController`
- any invoice status handling that checks approval validity

These rules should be enforced centrally so all approval paths behave consistently.

## Out of scope for the first release

- Email or SMS notifications for approval rejection or escalation.
- Special exception workflows for executive overrides.
- UI-only enforcement without backend validation.
- Changes to invoice creation or posting logic.

## Open questions (good prompts for clarifying questions)

- Should a Director count as a valid first approver for a 500,000+ invoice, or must the first approver always be a Manager or Director with authority to act?
- Are there any business exceptions for a Director approving an invoice above 500,000 without a second approver in emergency cases?
- Should the system display the remaining approval authority for a user in the UI, or only enforce it on submit?
- Do we want the rejection reason to be surfaced visibly in the UI as a user-friendly message, or only in the API response?
