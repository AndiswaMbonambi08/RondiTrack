using RondiTrack.Domain;
using Xunit;

namespace RondiTrack.Tests;

public class PayoutOrderBuilderTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);

    private static PayoutOrderMember M(string name, int day) => new(Guid.NewGuid(), name, T0.AddDays(day));

    private static Dictionary<Guid, DateTime> Paid(params PayoutOrderMember[] who) =>
        who.ToDictionary(w => w.UserId, _ => T0.AddDays(100));

    [Fact]
    public void Order_IsJoinOrder_NotTheOrderPassedIn()
    {
        var a = M("A", 0);
        var b = M("B", 1);
        var c = M("C", 2);

        var lines = PayoutOrderBuilder.Build(new[] { c, a, b }, Paid(), 1500);

        Assert.Equal(new[] { "A", "B", "C" }, lines.Select(l => l.FullName));
        Assert.Equal(new[] { 1, 2, 3 }, lines.Select(l => l.Position));
    }

    [Fact]
    public void NobodyPaid_FirstIsNext_RestWaiting()
    {
        var lines = PayoutOrderBuilder.Build(new[] { M("A", 0), M("B", 1), M("C", 2) }, Paid(), 1500);

        Assert.Equal(
            new[] { PayoutOrderStatus.Next, PayoutOrderStatus.Waiting, PayoutOrderStatus.Waiting },
            lines.Select(l => l.Status));
    }

    [Fact]
    public void FirstPaid_SecondIsNext_ThirdWaiting_AndPaidHasADate()
    {
        var a = M("A", 0);
        var lines = PayoutOrderBuilder.Build(new[] { a, M("B", 1), M("C", 2) }, Paid(a), 1500);

        Assert.Equal(
            new[] { PayoutOrderStatus.Paid, PayoutOrderStatus.Next, PayoutOrderStatus.Waiting },
            lines.Select(l => l.Status));
        Assert.NotNull(lines[0].PaidAtUtc);
        Assert.Null(lines[1].PaidAtUtc);
    }

    [Fact]
    public void EveryoneEarnsTheSamePot()
    {
        var lines = PayoutOrderBuilder.Build(new[] { M("A", 0), M("B", 1) }, Paid(), 100000);

        Assert.All(lines, l => Assert.Equal(100000, l.AmountCents));
    }

    [Fact]
    public void EveryonePaid_NobodyIsNext()
    {
        var a = M("A", 0);
        var b = M("B", 1);
        var lines = PayoutOrderBuilder.Build(new[] { a, b }, Paid(a, b), 1000);

        Assert.DoesNotContain(lines, l => l.Status == PayoutOrderStatus.Next);
    }

    [Fact]
    public void SecondPaidFirstNot_FirstIsStillNext()
    {
        var a = M("A", 0);
        var b = M("B", 1);
        var lines = PayoutOrderBuilder.Build(new[] { a, b }, Paid(b), 1000);

        Assert.Equal(new[] { PayoutOrderStatus.Next, PayoutOrderStatus.Paid }, lines.Select(l => l.Status));
    }

    [Fact]
    public void NoMembers_EmptyOrder()
    {
        Assert.Empty(PayoutOrderBuilder.Build(Array.Empty<PayoutOrderMember>(), Paid(), 0));
    }
}