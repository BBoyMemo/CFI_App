using CfiApp.Domain.Attendance;
using Shouldly;

namespace CfiApp.Tests.Domain;

public sealed class GeoDistanceTests
{
    [Fact]
    public void The_distance_from_a_point_to_itself_is_zero()
    {
        GeoDistance.HaversineMetres(53.365, -2.735, 53.365, -2.735).ShouldBe(0, 0.001);
    }

    [Fact]
    public void One_degree_of_latitude_is_roughly_111_kilometres()
    {
        var distance = GeoDistance.HaversineMetres(53.0, -2.735, 54.0, -2.735);

        distance.ShouldBeInRange(110_000, 112_000);
    }

    [Fact]
    public void A_short_known_distance_is_computed_within_a_few_metres()
    {
        // Two points roughly 500m apart on the Widnes site's latitude - hand-checked
        // against a mapping tool rather than derived from the formula under test.
        var distance = GeoDistance.HaversineMetres(53.3600, -2.7350, 53.3645, -2.7350);

        distance.ShouldBeInRange(490, 510);
    }
}
