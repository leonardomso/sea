using Sea.LoadTests;
using Xunit;

namespace Sea.LoadTests.Tests;

public sealed class LoadWorkloadPlanTests
{
    [Fact]
    public void FiveThousandClientsAssignExactlyOneThousandActiveRoles()
    {
        var plans = Enumerable.Range(0, 5_000)
            .Select(index => LoadWorkloadPlan.Create(index, 5_000, 1_000))
            .ToArray();

        Assert.Equal(1_000, plans.Count(plan => plan.Mode == LoadClientMode.Sailing));
        Assert.Equal(4_000, plans.Count(plan => plan.Mode == LoadClientMode.Dormant));
        Assert.Equal(5_000, plans.Select(plan => plan.ClientIndex).Distinct().Count());
    }

    [Fact]
    public void RoleAssignmentAndCourseAttemptsAreDeterministic()
    {
        var first = LoadWorkloadPlan.Create(73, 5_000, 1_000);
        var second = LoadWorkloadPlan.Create(73, 5_000, 1_000);

        Assert.Equal(first, second);
        Assert.Equal(
            first.CourseAttempts(currentX: 22, currentY: -17),
            second.CourseAttempts(currentX: 22, currentY: -17));
    }

    [Fact]
    public void CourseAttemptsStayInsideTheMapAndAwayFromTheCurrentPosition()
    {
        var plan = LoadWorkloadPlan.Create(73, 5_000, 1_000);

        var courses = plan.CourseAttempts(currentX: 92, currentY: -92);

        Assert.InRange(courses.Count, 1, 8);
        Assert.All(courses, course =>
        {
            Assert.InRange(course.X, -95f, 95f);
            Assert.InRange(course.Y, -95f, 95f);
            Assert.InRange(
                MathF.Sqrt(
                    MathF.Pow(course.X - 92f, 2) +
                    MathF.Pow(course.Y + 92f, 2)),
                120f,
                270f);
        });
    }

    [Fact]
    public void ConsecutiveCourseCyclesChooseDifferentDistributedDestinations()
    {
        var plans = Enumerable.Range(0, 1_000)
            .Select(index => LoadWorkloadPlan.Create(index, 5_000, 1_000))
            .ToArray();

        var firstCycle = plans.Select(plan => plan.CourseAttempts(0, 0, cycle: 0)[0])
            .ToArray();
        var secondCycle = plans.Select(plan => plan.CourseAttempts(0, 0, cycle: 1)[0])
            .ToArray();

        Assert.All(
            firstCycle.Zip(secondCycle),
            pair => Assert.NotEqual(pair.First, pair.Second));
        Assert.True(firstCycle.Select(course => (course.X, course.Y)).Distinct().Count() > 900);
        Assert.All(firstCycle.Concat(secondCycle), course =>
        {
            Assert.InRange(course.X, -95f, 95f);
            Assert.InRange(course.Y, -95f, 95f);
        });
    }

    [Theory]
    [InlineData(-1, 5_000, 1_000)]
    [InlineData(5_000, 5_000, 1_000)]
    [InlineData(0, 0, 0)]
    [InlineData(0, 5_000, 5_001)]
    public void InvalidWorkloadDimensionsAreRejected(
        int clientIndex,
        int totalClients,
        int activeClients)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LoadWorkloadPlan.Create(clientIndex, totalClients, activeClients));
    }
}
