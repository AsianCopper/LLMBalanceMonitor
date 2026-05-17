using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace LLMBalanceMonitor;

public class BalancePopup : Form
{
    public static string AppLanguage { get; set; } = "zh";

    private List<BalanceInfo> _data;
    private DateTime _updatedAt;
    private int _hoverIndex = -1;
    private static readonly Font ProviderFont = new("Segoe UI", 10, FontStyle.Regular, GraphicsUnit.Point);
    private static readonly Font BalanceFont = new("Segoe UI", 11, FontStyle.Bold, GraphicsUnit.Point);
    private static readonly Font HintFont = new("Segoe UI", 10, FontStyle.Regular, GraphicsUnit.Point);

    private const int PopupWidth = 260;
    private const int RowHeight = 32;
    private const int AccentHeight = 2;

    private static readonly Color BgColor = Color.FromArgb(28, 28, 34);
    private static readonly Color BorderColor = Color.FromArgb(60, 60, 70);
    private static readonly Color AccentColor = Color.FromArgb(0, 180, 120);
    private static readonly Color RowEvenColor = Color.FromArgb(34, 34, 40);
    private static readonly Color RowHoverColor = Color.FromArgb(45, 45, 52);
    private static readonly Color TextColor = Color.FromArgb(220, 220, 225);
    private static readonly Color GreenColor = Color.FromArgb(60, 200, 140);
    private static readonly Color YellowColor = Color.FromArgb(230, 180, 40);
    private static readonly Color RedColor = Color.FromArgb(220, 80, 70);
    private static readonly Color MutedColor = Color.FromArgb(140, 140, 150);

    public BalancePopup(List<BalanceInfo> data, DateTime updatedAt)
    {
        _data = data;
        _updatedAt = updatedAt;
        BuildForm();
    }

    public void UpdateData(List<BalanceInfo> data, DateTime updatedAt)
    {
        _data = data;
        _updatedAt = updatedAt;

        Height = CalcHeight();
        Reposition();
        Invalidate();
    }

    private string HintText => AppLanguage == "en" ? "Right-click tray to add API" : "右键图标添加API";

    private int CalcHeight() => (_data.Count > 0 ? _data.Count : 1) * RowHeight + AccentHeight;

    private void Reposition()
    {
        var screen = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(screen.Right - PopupWidth - 12, screen.Bottom - Height - 12);
    }

    private void BuildForm()
    {
        Height = CalcHeight();

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Width = PopupWidth;
        BackColor = BgColor;

        Reposition();

        int style = NativeMethods.GetWindowLong(Handle, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(Handle, NativeMethods.GWL_EXSTYLE, style | NativeMethods.WS_EX_DROPSHADOW);

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var bgBrush = new SolidBrush(BgColor))
            g.FillRectangle(bgBrush, ClientRectangle);

        using var borderPen = new Pen(BorderColor);
        g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

        using var accentPen = new Pen(AccentColor, AccentHeight);
        g.DrawLine(accentPen, 0, 0, Width, 0);

        if (_data.Count == 0)
        {
            string hint = HintText;
            using var hintBrush = new SolidBrush(MutedColor);
            var hintSize = g.MeasureString(hint, HintFont);
            g.DrawString(hint, HintFont, hintBrush,
                (Width - hintSize.Width) / 2,
                AccentHeight + (Height - AccentHeight - hintSize.Height) / 2);
            return;
        }

        int y = AccentHeight;
        for (int i = 0; i < _data.Count; i++)
        {
            var info = _data[i];
            var rowRect = new Rectangle(0, y, Width, RowHeight);

            bool isHover = i == _hoverIndex;
            using (var rowBrush = new SolidBrush(isHover ? RowHoverColor : (i % 2 == 0 ? BgColor : RowEvenColor)))
                g.FillRectangle(rowBrush, rowRect);

            if (i < _data.Count - 1)
            {
                using var linePen = new Pen(Color.FromArgb(45, 45, 52));
                g.DrawLine(linePen, 0, y + RowHeight, Width, y + RowHeight);
            }

            using (var nameBrush = new SolidBrush(TextColor))
                g.DrawString(info.Provider, ProviderFont, nameBrush, 12, y + 6);

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
                    Width - 12 - valueSize.Width, y + 5);
            }

            y += RowHeight;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int row = (e.Y - AccentHeight) / RowHeight;
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
