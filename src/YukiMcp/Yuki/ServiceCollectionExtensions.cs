using Microsoft.Extensions.DependencyInjection;
using YukiMcp.Configuration;

namespace YukiMcp.Yuki;

/// <summary>
/// Registers a generated WCF client (see Yuki/&lt;Service&gt;/&lt;Service&gt;Client.cs) for every
/// Yuki .asmx service, each pointed at the configured base URL and using the SOAP 1.1 endpoint
/// (matching the envelopes in the public Postman collection - the *Soap12 alternative generated
/// alongside it is unused). Registered transient, not singleton: a WCF client moves to the
/// Faulted state after certain errors and must be recreated, so tools get a fresh instance per
/// call instead of risking a wedged shared client.
/// </summary>
internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddYukiClients(this IServiceCollection services)
    {
        services.AddTransient(sp => new Accounting.AccountingSoapClient(
            Accounting.AccountingSoapClient.EndpointConfiguration.AccountingSoap, $"{BaseUrl(sp)}Accounting.asmx"));
        services.AddTransient(sp => new AccountingInfo.AccountingInfoSoapClient(
            AccountingInfo.AccountingInfoSoapClient.EndpointConfiguration.AccountingInfoSoap, $"{BaseUrl(sp)}AccountingInfo.asmx"));
        services.AddTransient(sp => new Archive.ArchiveSoapClient(
            Archive.ArchiveSoapClient.EndpointConfiguration.ArchiveSoap, $"{BaseUrl(sp)}Archive.asmx"));
        services.AddTransient(sp => new Backoffice.BackofficeSoapClient(
            Backoffice.BackofficeSoapClient.EndpointConfiguration.BackofficeSoap, $"{BaseUrl(sp)}Backoffice.asmx"));
        services.AddTransient(sp => new ChangeDigest.ChangeDigestSoapClient(
            ChangeDigest.ChangeDigestSoapClient.EndpointConfiguration.ChangeDigestSoap, $"{BaseUrl(sp)}ChangeDigest.asmx"));
        services.AddTransient(sp => new Contact.ContactSoapClient(
            Contact.ContactSoapClient.EndpointConfiguration.ContactSoap, $"{BaseUrl(sp)}Contact.asmx"));
        services.AddTransient(sp => new Domains.DomainsSoapClient(
            Domains.DomainsSoapClient.EndpointConfiguration.DomainsSoap, $"{BaseUrl(sp)}Domains.asmx"));
        services.AddTransient(sp => new FiscalTable.FiscalTableSoapClient(
            FiscalTable.FiscalTableSoapClient.EndpointConfiguration.FiscalTableSoap, $"{BaseUrl(sp)}FiscalTable.asmx"));
        services.AddTransient(sp => new Integration.IntegrationSoapClient(
            Integration.IntegrationSoapClient.EndpointConfiguration.IntegrationSoap, $"{BaseUrl(sp)}Integration.asmx"));
        services.AddTransient(sp => new Pettycash.PettyCashSoapClient(
            Pettycash.PettyCashSoapClient.EndpointConfiguration.PettyCashSoap, $"{BaseUrl(sp)}Pettycash.asmx"));
        services.AddTransient(sp => new Projects.ProjectsSoapClient(
            Projects.ProjectsSoapClient.EndpointConfiguration.ProjectsSoap, $"{BaseUrl(sp)}projects.asmx"));
        services.AddTransient(sp => new Sales.SalesSoapClient(
            Sales.SalesSoapClient.EndpointConfiguration.SalesSoap, $"{BaseUrl(sp)}Sales.asmx"));
        services.AddTransient(sp => new Vat.VATSoapClient(
            Vat.VATSoapClient.EndpointConfiguration.VATSoap, $"{BaseUrl(sp)}Vat.asmx"));
        return services;
    }

    private static string BaseUrl(IServiceProvider sp) => sp.GetRequiredService<YukiServerOptions>().BaseUrl;
}
