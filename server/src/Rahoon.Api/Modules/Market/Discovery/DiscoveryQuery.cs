using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Market.Discovery;

/// <summary>A published opportunity with its published terms version: the only thing public discovery ever reads.</summary>
public sealed class PublishedRow
{
    public required Opportunity O { get; init; }
    public required OpportunityTerms T { get; init; }
}

/// <summary>
/// The only query builder of public discovery (Phase 2). Visibility (status Published, its published terms, public coordinates
/// only) is applied here, so the list, its count, the facets, the map, saved searches and alerts always see the same set.
/// Budget rules are the SQL twin of <see cref="Affordability.Classify"/>. Must run inside a system scope (public read).
/// </summary>
public static class DiscoveryQuery
{
    public static IQueryable<PublishedRow> Published(RahoonDbContext db) =>
        db.Opportunities.Where(o => o.Status == OpportunityStatus.Published)
            .Join(db.OpportunityTerms, o => o.PublishedTermsId, t => t.Id, (o, t) => new PublishedRow { O = o, T = t });

    /// <summary>Everything except the budget: location, property, delivery, features, track, map window.</summary>
    public static IQueryable<PublishedRow> Attributes(IQueryable<PublishedRow> q, SearchCriteria c, bool skipCity = false, bool skipTypes = false)
    {
        if (!skipCity && c.Cities.Count > 0) { var cities = c.Cities.ToList(); q = q.Where(x => cities.Contains(x.O.City)); }
        if (!skipTypes && c.Types.Count > 0) { var types = c.Types.ToList(); q = q.Where(x => types.Contains(x.O.PropertyType)); }
        if (c.District is { } district) { var p = $"%{Escape(district)}%"; q = q.Where(x => x.O.District != null && EF.Functions.ILike(x.O.District, p)); }
        if (c.Project is { } project)
        {
            var p = $"%{Escape(project)}%";
            // The developer's name is searchable only when it comes from the directory (the same rule as the card).
            q = q.Where(x => (x.O.Project != null && EF.Functions.ILike(x.O.Project, p))
                             || (x.O.DeveloperPartyId != null && x.O.DeveloperName != null && EF.Functions.ILike(x.O.DeveloperName, p)));
        }
        if (c.Developer is { } dev) q = q.Where(x => x.O.DeveloperPartyId == dev);
        if (c.MinArea is { } minA) q = q.Where(x => x.O.Area != null && x.O.Area >= minA);
        if (c.MaxArea is { } maxA) q = q.Where(x => x.O.Area != null && x.O.Area <= maxA);
        if (c.Bedrooms is { } beds) q = q.Where(x => x.O.Bedrooms != null && x.O.Bedrooms >= beds);
        if (c.Bathrooms is { } baths) q = q.Where(x => x.O.Bathrooms != null && x.O.Bathrooms >= baths);
        if (c.Readiness is { } r) q = q.Where(x => x.O.Readiness == r);
        // Delivery: only opportunities with a known delivery month match a delivery range (YYYY-MM compares as text).
        if (c.DeliveryFrom is { } from) q = q.Where(x => x.O.DeliveryMonth != null && string.Compare(x.O.DeliveryMonth, from) >= 0);
        if (c.DeliveryTo is { } to) q = q.Where(x => x.O.DeliveryMonth != null && string.Compare(x.O.DeliveryMonth, to) <= 0);
        foreach (var f in c.Features) q = q.Where(x => x.O.Features.Contains(f));
        if (c.Track is { } track) q = q.Where(x => x.O.Track == track);
        if (c.Bounds is { } b)
            q = q.Where(x => x.O.PublicLatitude != null && x.O.PublicLongitude != null
                             && x.O.PublicLatitude >= b.South && x.O.PublicLatitude <= b.North && x.O.PublicLongitude >= b.West && x.O.PublicLongitude <= b.East);
        return q;
    }

    /// <summary>Rows that pass every given capacity rule (all figures known and within).</summary>
    public static Expression<Func<PublishedRow, bool>> Passes(CapacityProfile cap)
    {
        Expression<Func<PublishedRow, bool>> e = x => true;
        if (cap.AvailableNow is { } a)
        {
            e = And(e, x => x.T.DueNow != null && x.T.DueNow <= a);
            e = And(e, x => x.T.LargestExtraPayment == null || (x.T.DueNow != null && x.T.LargestExtraPayment <= a - x.T.DueNow));
        }
        if (cap.MaxTotal is { } max) e = And(e, x => x.T.PurchaseTotal != null && x.T.PurchaseTotal <= max);
        if (cap.ComfortMonthly is { } cm)
        {
            var cm12 = cm * 12;
            e = And(e, x => x.T.FutureBalance == 0
                            || (x.T.InstallmentMonthlyEquivalent != null && x.T.InstallmentMonthlyEquivalent <= cm && x.T.ScheduleKnown
                                && (x.T.AnnualExtraPayment == null || x.T.AnnualExtraPayment <= 0 || x.T.InstallmentMonthlyEquivalent * 12 + x.T.AnnualExtraPayment <= cm12)));
        }
        if (cap.MaxTermMonths is { } m) e = And(e, x => x.T.FutureBalance == 0 || (x.T.RemainingMonths != null && x.T.RemainingMonths <= m));
        return e;
    }

    /// <summary>Rows where at least one given capacity rule fails on known figures.</summary>
    public static Expression<Func<PublishedRow, bool>> Fails(CapacityProfile cap)
    {
        Expression<Func<PublishedRow, bool>> e = x => false;
        if (cap.AvailableNow is { } a)
        {
            e = Or(e, x => x.T.DueNow != null && x.T.DueNow > a);
            e = Or(e, x => x.T.LargestExtraPayment != null && x.T.DueNow != null && x.T.LargestExtraPayment > a - x.T.DueNow);
        }
        if (cap.MaxTotal is { } max) e = Or(e, x => x.T.PurchaseTotal != null && x.T.PurchaseTotal > max);
        if (cap.ComfortMonthly is { } cm)
        {
            var cm12 = cm * 12;
            e = Or(e, x => x.T.FutureBalance != 0 && x.T.InstallmentMonthlyEquivalent != null
                           && (x.T.InstallmentMonthlyEquivalent > cm
                               || (x.T.ScheduleKnown && x.T.AnnualExtraPayment > 0 && x.T.InstallmentMonthlyEquivalent * 12 + x.T.AnnualExtraPayment > cm12)));
        }
        if (cap.MaxTermMonths is { } m) e = Or(e, x => x.T.FutureBalance != 0 && x.T.RemainingMonths != null && x.T.RemainingMonths > m);
        return e;
    }

    /// <summary>The strict budget: passing rows, and the query of rows excluded only because a figure is unknown («incomplete»).</summary>
    public static (IQueryable<PublishedRow> Passing, IQueryable<PublishedRow> Incomplete) Budget(IQueryable<PublishedRow> q, CapacityProfile cap)
    {
        if (!cap.Any) return (q, q.Where(_ => false));
        var pass = Passes(cap);
        var fail = Fails(cap);
        var notPass = Expression.Lambda<Func<PublishedRow, bool>>(Expression.Not(pass.Body), pass.Parameters);
        var notFail = Expression.Lambda<Func<PublishedRow, bool>>(Expression.Not(Rebind(fail, pass.Parameters[0])), pass.Parameters);
        return (q.Where(pass), q.Where(notPass).Where(notFail));
    }

    /// <summary>
    /// Every sort ends with the id, so pages never overlap or skip. «relevance»: preferences matched (with a buyer profile), then
    /// complete and verified figures first, then the lowest cash now when a budget is given, then the newest publication.
    /// </summary>
    public static IOrderedQueryable<PublishedRow> Order(IQueryable<PublishedRow> q, string sort, bool budget, RankingPreferences? prefs) => sort switch
    {
        "now" => q.OrderBy(x => x.T.DueNow == null).ThenBy(x => x.T.DueNow).ThenByDescending(x => x.O.PublishedAt).ThenBy(x => x.O.Id),
        "total" => q.OrderBy(x => x.T.PurchaseTotal == null).ThenBy(x => x.T.PurchaseTotal).ThenByDescending(x => x.O.PublishedAt).ThenBy(x => x.O.Id),
        "relevance" => Relevance(q, budget, prefs),
        _ => q.OrderByDescending(x => x.O.PublishedAt).ThenBy(x => x.O.Id),
    };

    private static IOrderedQueryable<PublishedRow> Relevance(IQueryable<PublishedRow> q, bool budget, RankingPreferences? prefs)
    {
        IOrderedQueryable<PublishedRow> o = prefs is { Any: true } p
            ? q.OrderByDescending(PreferenceScore(p)).ThenBy(x => x.T.Quality == "complete_verified" ? 0 : x.T.Quality == "complete_estimate" ? 1 : 2)
            : q.OrderBy(x => x.T.Quality == "complete_verified" ? 0 : x.T.Quality == "complete_estimate" ? 1 : 2);
        if (budget) o = o.ThenBy(x => x.T.DueNow == null).ThenBy(x => x.T.DueNow);
        return o.ThenByDescending(x => x.O.PublishedAt).ThenBy(x => x.O.Id);
    }

    /// <summary>Number of ranking preferences an opportunity meets (SQL). <see cref="Matching.PreferenceReasons"/> explains each.</summary>
    public static Expression<Func<PublishedRow, int>> PreferenceScore(RankingPreferences p)
    {
        var minA = p.AreaMin;
        var maxA = p.AreaMax;
        var hasArea = p.AreaMin is not null || p.AreaMax is not null;
        var beds = p.BedroomsMin ?? -1;
        var hasBeds = p.BedroomsMin is not null;
        var readiness = p.Readiness is "ready" or "under_construction" ? p.Readiness : null;
        var deliveryBy = p.DeliveryBy;
        var districts = p.Districts.ToList();
        return x =>
            (hasArea && x.O.Area != null && (minA == null || x.O.Area >= minA) && (maxA == null || x.O.Area <= maxA) ? 1 : 0)
            + (hasBeds && x.O.Bedrooms != null && x.O.Bedrooms >= beds ? 1 : 0)
            + (readiness != null && x.O.Readiness == readiness ? 1 : 0)
            + (deliveryBy != null && (x.O.Readiness == "ready" || (x.O.DeliveryMonth != null && string.Compare(x.O.DeliveryMonth, deliveryBy) <= 0)) ? 1 : 0)
            + (districts.Count > 0 && x.O.District != null && districts.Contains(x.O.District) ? 1 : 0);
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static Expression<Func<PublishedRow, bool>> And(Expression<Func<PublishedRow, bool>> a, Expression<Func<PublishedRow, bool>> b) =>
        Expression.Lambda<Func<PublishedRow, bool>>(Expression.AndAlso(a.Body, Rebind(b, a.Parameters[0])), a.Parameters);

    private static Expression<Func<PublishedRow, bool>> Or(Expression<Func<PublishedRow, bool>> a, Expression<Func<PublishedRow, bool>> b) =>
        Expression.Lambda<Func<PublishedRow, bool>>(Expression.OrElse(a.Body, Rebind(b, a.Parameters[0])), a.Parameters);

    private static Expression Rebind(LambdaExpression e, ParameterExpression p) => new Swap(e.Parameters[0], p).Visit(e.Body);

    private sealed class Swap(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
