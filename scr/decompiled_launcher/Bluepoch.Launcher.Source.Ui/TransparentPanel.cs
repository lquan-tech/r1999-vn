using System.Drawing;
using System.Windows.Forms;

namespace Bluepoch.Launcher.Source.Ui;

public class TransparentPanel : Panel
{
	public int Alpha { get; set; } = 128;

	public Color FillColor { get; set; } = Color.Black;

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		using SolidBrush brush = new SolidBrush(Color.FromArgb(Alpha, FillColor));
		e.Graphics.FillRectangle(brush, ClientRectangle);
	}
}
