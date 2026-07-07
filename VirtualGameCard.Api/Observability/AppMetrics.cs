using Prometheus;

namespace VirtualGameCard.Api.Observability;

public static class AppMetrics
{
    public static readonly Counter AuthEvents = Metrics.CreateCounter(
        "vgc_auth_events_total",
        "Total de eventos de autenticação por tipo e resultado.",
        new CounterConfiguration { LabelNames = ["event", "result"] }
    );

    public static readonly Counter PurchaseEvents = Metrics.CreateCounter(
        "vgc_purchase_events_total",
        "Total de eventos de compra por plataforma, método, status e origem.",
        new CounterConfiguration { LabelNames = ["platform", "method", "status", "source"] }
    );

    public static readonly Counter PaymentWebhookEvents = Metrics.CreateCounter(
        "vgc_payment_webhook_events_total",
        "Total de webhooks de pagamento processados por status e resultado.",
        new CounterConfiguration { LabelNames = ["status", "result"] }
    );

    public static readonly Counter SupportTickets = Metrics.CreateCounter(
        "vgc_support_tickets_total",
        "Total de chamados de suporte por categoria e resultado.",
        new CounterConfiguration { LabelNames = ["category", "result"] }
    );

    public static readonly Gauge AppInfo = Metrics.CreateGauge(
        "vgc_app_info",
        "Informações da aplicação VirtualGameCard.",
        new GaugeConfiguration { LabelNames = ["service", "environment"] }
    );

    public static void SetAppInfo(string environment) =>
        AppInfo.WithLabels("virtualgamecard-api", environment).Set(1);
}
