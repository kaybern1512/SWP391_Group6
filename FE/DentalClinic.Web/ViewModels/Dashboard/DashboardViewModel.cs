namespace DentalClinic.Web.ViewModels.Dashboard;

public class DashboardViewModel
{
    public string Role { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    
    // 4 KPI / Metric Stat Cards
    public List<StatCardItem> StatCards { get; set; } = new();

    // Quick Action Cards matching precise Role Use Cases
    public List<DashboardCardItem> Cards { get; set; } = new();

    // Contextual Empty State for Workspaces
    public EmptyStateViewModel EmptyState { get; set; } = new();
}

public class StatCardItem
{
    public string Title { get; set; } = string.Empty;
    public string Value { get; set; } = "--";
    public string Note { get; set; } = "Waiting for backend data";
    public string Icon { get; set; } = "bi-bar-chart";
    public string ColorClass { get; set; } = "primary";
}

public class DashboardCardItem
{
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string ColorClass { get; set; } = "primary";
    public string Description { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public string? ActionText { get; set; }
    public string? BadgeText { get; set; }
}

public class EmptyStateViewModel
{
    public string Title { get; set; } = "No data available yet";
    public string Description { get; set; } = "Data will be displayed here once connected to the clinic backend services.";
    public string Icon { get; set; } = "bi-clipboard-x";
    public string? ActionUrl { get; set; }
    public string? ActionText { get; set; }
}
