using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace LLMBalanceMonitor;

public class BalancePopup : Form
{
    private readonly List<BalanceInfo> _data;
    private readonly DateTime _updatedAt;
    private int _hoverIndex = -1;
    private static readonly Font ProviderFont = new("Segoe UI", 10, FontStyle.Regular, GraphicsUnit.Point);
    private static readonly Font BalanceFont = new("Segoe UI", 11, FontStyle.Bold, GraphicsUnit.Point);
    private static readonly Font HeaderFont = new("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
    private static readonly Font StatusFont = new("Segoe UI", 8, FontStyle.Regular, GraphicsUnit.Point);

    private const int PopupWidth = 280;
    private const int RowHeight = 36;
    private const int HeaderHeight = 38;
    private const int FooterHeight = 28;
    private const int PopupPadding = 14;

    private static readonly Color BgColor = Color.FromArgb(32, 32, 38);
    private static readonly Color BorderColor = Color.FromArgb(60, 60, 70);
    private static readonly Color HeaderColor = Color.FromArgb(22, 22, 26);
    private static readonly Color RowEvenColor = Color.FromArgb(38, 38, 44);
    private static readonly Color RowHoverColor = Color.FromArgb(45, 45, 52);
    private static readonly Color TextColor = Color.FromArgb(220, 220, 225);
    private static readonly Color MutedColor = Color.FromArgb(140, 140, 150);
    private static readonly Color GreenColor = Color.FromArgb(60, 200, 140);
    private static readonly Color YellowColor = Color.FromArgb(230, 180, 40);
    private static readonly Color RedColor = Color.FromArgb(220, 80, 70);

    public BalancePopup(List<BalanceInfo> data, DateTime updatedAt)
    {
        _data = data;
        _updatedAt = updatedAt;
        BuildForm();
    }

    private void BuildForm()
    {
        int rowCount = Math.Max(_data.Count, 1);
        int popupHeight = HeaderHeight + rowCount * RowHeight + FooterHeight;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(PopupWidth, popupHeight);
        BackColor = BgColor;

        // Position near system tray (bottom-right of primary screen)
        var screen = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(screen.Right - PopupWidth - 8, screen.Bottom - popupHeight - 4);

        // Enable drop shadow
        int style = NativeMethods.GetWindowLong(Handle, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(Handle, NativeMethods.GWL_EXSTYLE, style | NativeMethods.WS_EX_DROPSHADOW);

        // Click outside to close
        Deactivate += (_, _) => Close();

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Background
        using (var bgBrush = new SolidBrush(BgColor))
            g.FillRectangle(bgBrush, ClientRectangle);

        // Border
        using var borderPen = new Pen(BorderColor);
        g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

        // Header
        var headerRect = new Rectangle(0, 0, Width, HeaderHeight);
        using (var headerBrush = new SolidBrush(HeaderColor))
            g.FillRectangle(headerBrush, headerRect);
        using var headerPen = new Pen(BorderColor);
        g.DrawLine(headerPen, 0, HeaderHeight, Width, HeaderHeight);

        // Header text
        using (var titleBrush = new SolidBrush(TextColor))
            g.DrawString("Balance Monitor", HeaderFont, titleBrush, PopupPadding, 10);

        // Auto-refresh dot
        int dotSize = 7;
        int dotX = Width - PopupPadding - dotSize - 2;
        int dotY = 14;
        using var dotBrush = new SolidBrush(GreenColor);
        g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);

        // Provider rows
        int y = HeaderHeight;
        for (int i = 0; i < _data.Count; i++)
        {
            var info = _data[i];
            var rowRect = new Rectangle(0, y, Width, RowHeight);

            // Row background
            bool isHover = i == _hoverIndex;
            using (var rowBrush = new SolidBrush(isHover ? RowHoverColor : (i % 2 == 0 ? BgColor : RowEvenColor)))
                g.FillRectangle(rowBrush, rowRect);

            if (i < _data.Count - 1)
            {
                using var linePen = new Pen(BorderColor);
                g.DrawLine(linePen, PopupPadding, y + RowHeight, Width - PopupPadding, y + RowHeight);
            }

            // Provider name
            using (var nameBrush = new SolidBrush(TextColor))
                g.DrawString(info.Provider, ProviderFont, nameBrush, PopupPadding, y + 9);

            // Balance value with color
            Color valueColor;
            string valueText;

            if (info.Status == "Error")
            {
                valueColor = RedColor;
                valueText = "ERR";
            }
            else if (info.Balance.HasValue)
            {
                string prefix = info.Currency == "CNY" ? "¥" : "$";
                valueText = $"{prefix}{info.Balance:F2}";

                if (info.Balance < 1) valueColor = RedColor;
                else if (info.Balance < 10) valueColor = YellowColor;
                else valueColor = GreenColor;
            }
            else if (info.Provider == "Gemini")
            {
                valueColor = GreenColor;
                valueText = "✓";
            }
            else
            {
                valueColor = MutedColor;
                valueText = "-";
            }

            using (var valueBrush = new SolidBrush(valueColor))
            {
                var valueSize = g.MeasureString(valueText, BalanceFont);
                g.DrawString(valueText, BalanceFont, valueBrush,
                    Width - PopupPadding - valueSize.Width, y + 8);
            }

            // Usage subtitle for OpenRouter
            if (info.Usage.HasValue && info.Usage > 0)
            {
                string usageText = $"used: ${info.Usage:F4}";
                using var usageBrush = new SolidBrush(MutedColor);
                g.DrawString(usageText, StatusFont, usageBrush,
                    Width - PopupPadding - 100, y + RowHeight - 16);
            }

            y += RowHeight;
        }

        // Footer
        var footerRect = new Rectangle(0, y, Width, FooterHeight);
        using (var footerBrush = new SolidBrush(HeaderColor))
            g.FillRectangle(footerBrush, footerRect);

        string footer = $"Updated {_updatedAt:HH:mm:ss}  ·  " +
                        $"{_data.Count(r => r.Status is "OK" or "Connected" or "Free Tier")}/{_data.Count} OK";
        using (var footerBrush = new SolidBrush(MutedColor))
            g.DrawString(footer, StatusFont, footerBrush, PopupPadding, y + 7);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int row = (e.Y - HeaderHeight) / RowHeight;
        int newHover = (row >= 0 && row < _data.Count) ? row : -1;
        if (newHover != _hoverIndex)
        {
            _hoverIndex = newHover;
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hoverIndex = -1;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= NativeMethods.CS_DROPSHADOW;
            return cp;
        }
    }
}

internal static class NativeMethods
{
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_DROPSHADOW = 0x00020000;
    public const int CS_DROPSHADOW = 0x00020000;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
