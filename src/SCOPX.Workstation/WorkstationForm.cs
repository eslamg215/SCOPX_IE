using System.Net.Http;
using System.Diagnostics;

namespace SCOPX.Workstation;

public sealed class WorkstationForm : Form
{
    private readonly TextBox _apiUrl = new() { Text = "http://localhost:5000", Width = 360 };
    private readonly Label _status = new() { Text = "Not connected", AutoSize = true, ForeColor = Color.DarkOrange };
    private readonly Button _check = new() { Text = "Check API", AutoSize = true };
    private readonly Button _openDemo = new() { Text = "Open Offline Demo", AutoSize = true };
    private readonly Label _warning = new() { Text = "DEVELOPMENT SHELL — NOT FOR CLINICAL USE OR REAL PATIENT DATA", AutoSize = true, ForeColor = Color.Firebrick, Font = new Font("Segoe UI", 9, FontStyle.Bold) };

    public WorkstationForm()
    {
        Text = "SCOPX.I.E — Endoscopy Workstation (Development)";
        MinimumSize = new Size(900, 600);
        Size = new Size(1120, 720);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(14, 22, 38);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);

        var header = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = Color.FromArgb(20, 34, 56) };
        var brand = new Label { Text = "SCOPX.I.E", Dock = DockStyle.Left, Width = 250, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(22, 0, 0, 0), Font = new Font("Segoe UI", 20, FontStyle.Bold), ForeColor = Color.FromArgb(64, 210, 245) };
        var subtitle = new Label { Text = "ENDOSCOPY WORKSTATION  •  DEV PREVIEW", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.LightSteelBlue };
        header.Controls.Add(subtitle); header.Controls.Add(brand);

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(28), BackColor = BackColor };
        var connectionCard = new Panel { Dock = DockStyle.Top, Height = 160, Padding = new Padding(20), BackColor = Color.FromArgb(24, 38, 60) };
        var title = new Label { Text = "API Connection", AutoSize = true, Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.White, Location = new Point(20, 16) };
        var urlLabel = new Label { Text = "Server URL", AutoSize = true, ForeColor = Color.LightSteelBlue, Location = new Point(22, 62) };
        _apiUrl.Location = new Point(22, 88); _apiUrl.BackColor = Color.FromArgb(10, 18, 32); _apiUrl.ForeColor = Color.White; _apiUrl.BorderStyle = BorderStyle.FixedSingle;
        _check.Location = new Point(398, 86); _check.BackColor = Color.FromArgb(0, 150, 200); _check.ForeColor = Color.White; _check.FlatStyle = FlatStyle.Flat;
        _openDemo.Location = new Point(520, 86); _openDemo.BackColor = Color.FromArgb(34, 145, 115); _openDemo.ForeColor = Color.White; _openDemo.FlatStyle = FlatStyle.Flat;
        _status.Location = new Point(22, 125);
        _check.Click += async (_, _) => await CheckApiAsync();
        _openDemo.Click += (_, _) => OpenOfflineDemo();
        connectionCard.Controls.Add(title); connectionCard.Controls.Add(urlLabel); connectionCard.Controls.Add(_apiUrl); connectionCard.Controls.Add(_check); connectionCard.Controls.Add(_openDemo); connectionCard.Controls.Add(_status);

        var featurePanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 250, Padding = new Padding(0, 24, 0, 0), FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        featurePanel.Controls.Add(FeatureCard("Patients", "Patient management UI — not connected", "patients"));
        featurePanel.Controls.Add(FeatureCard("Procedures", "Procedure workflow — not connected", "procedures"));
        featurePanel.Controls.Add(FeatureCard("Reports", "Draft/review reporting — not connected", "reports"));
        featurePanel.Controls.Add(FeatureCard("Media", "Endoscopy media — not connected", "media"));
        body.Controls.Add(featurePanel); body.Controls.Add(connectionCard);
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(18, 8, 18, 8), BackColor = Color.FromArgb(20, 34, 56) };
        _warning.Dock = DockStyle.Left; footer.Controls.Add(_warning);
        Controls.Add(body); Controls.Add(footer); Controls.Add(header);
    }

    private Control FeatureCard(string heading, string description, string key)
    {
        var card = new Panel { Width = 230, Height = 120, BackColor = Color.FromArgb(24, 38, 60), Margin = new Padding(0, 0, 12, 12), Padding = new Padding(14), Tag = key };
        var h = new Label { Text = heading, AutoSize = true, ForeColor = Color.FromArgb(64, 210, 245), Font = new Font("Segoe UI", 13, FontStyle.Bold), Location = new Point(14, 12) };
        var d = new Label { Text = description, AutoSize = false, Width = 195, Height = 55, ForeColor = Color.LightSteelBlue, Location = new Point(14, 44) };
        card.Controls.Add(h); card.Controls.Add(d); return card;
    }


    private void OpenOfflineDemo()
    {
        var demoPath = Path.Combine(AppContext.BaseDirectory, "DemoWeb", "index.html");
        if (!File.Exists(demoPath))
        {
            MessageBox.Show("Offline demo files were not found beside the workstation app.", "SCOPX.I.E Demo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = demoPath, UseShellExecute = true });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show("Could not open the offline demo in the default browser.", "SCOPX.I.E Demo", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task CheckApiAsync()
    {
        if (!Uri.TryCreate(_apiUrl.Text.Trim(), UriKind.Absolute, out var baseUri) || !ApiEndpointPolicy.IsAllowedBaseUri(baseUri))
        {
            _status.Text = "Use HTTPS for remote servers; HTTP is allowed only on this computer.";
            _status.ForeColor = Color.OrangeRed;
            return;
        }
        _check.Enabled = false; _status.Text = "Checking..."; _status.ForeColor = Color.Gold;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var healthUri = new Uri(baseUri.ToString().TrimEnd('/') + "/health/ready");
            using var response = await client.GetAsync(healthUri);
            _status.Text = response.IsSuccessStatusCode ? $"API and database reachable ({(int)response.StatusCode}) — connectivity only" : $"Readiness response: {(int)response.StatusCode}";
            _status.ForeColor = response.IsSuccessStatusCode ? Color.LightGreen : Color.OrangeRed;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _status.Text = "Connection failed / timed out"; _status.ForeColor = Color.OrangeRed;
        }
        finally { _check.Enabled = true; }
    }
}
