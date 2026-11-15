// The rotation for one stokvel: who has been paid, who is next, who is waiting.
// The server decides all of it. Clients show it as sent and never sort, count or work out "next".
namespace RondiTrack.Domain;

public enum PayoutOrderStatus
{
    Paid,
    Next,
    Waiting
}

public sealed record PayoutOrderMember(Guid UserId, string FullName, DateTime JoinedAtUtc);

public sealed record PayoutOrderLine(
    Guid UserId,
    string FullName,
    int Position,
    long AmountCents,
    DateTime? PaidAtUtc,
    PayoutOrderStatus Status);

public static class PayoutOrderBuilder
{
    // Rotation order is the order people joined (ties broken by user id so it is stable).
    // The first member who has not been paid is Next; everyone after them is Waiting.
    public static IReadOnlyList<PayoutOrderLine> Build(
        IEnumerable<PayoutOrderMember> members,
        IReadOnlyDictionary<Guid, DateTime> paidAtByUser,
        long potCents)
    {
        var ordered = members.OrderBy(m => m.JoinedAtUtc).ThenBy(m => m.UserId).ToList();
        var lines = new List<PayoutOrderLine>(ordered.Count);
        var nextTaken = false;

        for (var i = 0; i < ordered.Count; i++)
        {
            var m = ordered[i];
            if (paidAtByUser.TryGetValue(m.UserId, out var paidAt))
            {
                lines.Add(new PayoutOrderLine(m.UserId, m.FullName, i + 1, potCents, paidAt, PayoutOrderStatus.Paid));
            }
            else if (!nextTaken)
            {
                nextTaken = true;
                lines.Add(new PayoutOrderLine(m.UserId, m.FullName, i + 1, potCents, null, PayoutOrderStatus.Next));
            }
            else
            {
                lines.Add(new PayoutOrderLine(m.UserId, m.FullName, i + 1, potCents, null, PayoutOrderStatus.Waiting));
            }
        }

        return lines;
    }
}