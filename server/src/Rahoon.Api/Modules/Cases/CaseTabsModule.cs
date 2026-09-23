using Rahoon.Api.Modules.Assessment;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Imports;

namespace Rahoon.Api.Modules.Cases;

/// <summary>
/// B3 case tabs (L06–L12), case audit log (L24), bulk import (L04) and the cancellation
/// maker-checker (C01). Registered from <see cref="ModuleRegistry"/> with a single line.
/// </summary>
public static class CaseTabsModule
{
    public static IEndpointRouteBuilder MapCaseTabs(this IEndpointRouteBuilder app)
    {
        CasePartyEndpoints.Map(app);
        CaseFinanceEndpoints.Map(app);
        CasePropertyEndpoints.Map(app);
        DocumentRequestPreviewEndpoints.Map(app);
        ValuationEndpoints.Map(app);
        AnalysisEndpoints.Map(app);
        CaseAuditEndpoints.Map(app);
        CancellationEndpoints.Map(app);
        ImportEndpoints.Map(app);
        return app;
    }
}
