using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Bluepoch.Launcher.App;
using Bluepoch.Launcher.Config;
using Bluepoch.Launcher.Constants;

namespace Bluepoch.Launcher.Source.Ui;

public class NaticeWarningDialog : Form
{
	private Image backgroundImage;

	private IContainer components;

	public NaticeWarningDialog(string message, Action action)
	{
		NaticeWarningDialog naticeWarningDialog = this;
		InitializeComponent();
		SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint, value: true);
		FormBorderStyle = FormBorderStyle.None;
		ShowInTaskbar = false;
		StartPosition = FormStartPosition.CenterScreen;
		backgroundImage = Image.FromFile(Path.Combine(Application.StartupPath, "Resources", "dialog_bg.png"));
		Label value = new Label
		{
			Text = Bluepoch.Launcher.Constants.Common.LocalDialogTitle[AppSettings.Instance.Language_index],
			Size = new Size(Width, 48),
			TextAlign = ContentAlignment.MiddleCenter,
			Font = new Font(LauncherApplication.GetInstance().GetFontStyle(), 12f, FontStyle.Bold),
			ForeColor = ColorTranslator.FromHtml("#000000"),
			BackColor = Color.Transparent
		};
		Controls.Add(value);
		Label value2 = new Label
		{
			Text = message,
			Font = new Font(LauncherApplication.GetInstance().GetFontStyle(), 11f),
			ForeColor = ColorTranslator.FromHtml("#333333"),
			Size = new Size(Width, Height / 2),
			TextAlign = ContentAlignment.MiddleCenter,
			BackColor = Color.Transparent,
			Location = new Point(0, (Height - Height / 2) / 2 - 18),
			Padding = new Padding(24, 0, 24, 0),
			AutoSize = false
		};
		Controls.Add(value2);
		Button button = new Button
		{
			Size = new Size(228, 56),
			Location = new Point((Width - 228) / 2, Height - 68 - 8),
			BackColor = ColorTranslator.FromHtml("#c66030"),
			ForeColor = ColorTranslator.FromHtml("#ffffff"),
			Text = (Bluepoch.Launcher.Constants.Common.LocalDialogConfirmButtonText.TryGetValue(AppSettings.Instance.Language_index, out var value3) ? value3 : "Confirm"),
			Font = new Font(LauncherApplication.GetInstance().GetFontStyle(), 12.5f),
			Cursor = Cursors.Hand
		};
		button.FlatStyle = FlatStyle.Flat;
		button.FlatAppearance.BorderSize = 0;
		button.FlatAppearance.BorderColor = Color.DarkOrange;
		button.BringToFront();
		button.MouseEnter += SureButton_MouseEnter;
		button.MouseLeave += SureButton_MouseLeave;
		button.MouseDown += SureButton_MouseDown;
		button.Click += (object? o, EventArgs e) =>
		{
			try
			{
				action();
				naticeWarningDialog.DialogResult = DialogResult.OK;
				naticeWarningDialog.Close();
			}
			catch (Exception ex)
			{
				Console.WriteLine("执行确认动作时出错: " + ex.Message);
				naticeWarningDialog.DialogResult = DialogResult.OK;
				naticeWarningDialog.Close();
			}
		};
		Controls.Add(button);
	}

	private void SureButton_MouseEnter(object sender, EventArgs e)
	{
		if (sender is Button button)
		{
			button.BackColor = ColorTranslator.FromHtml("#dc743c");
		}
	}

	private void SureButton_MouseLeave(object sender, EventArgs e)
	{
		Button obj = sender as Button;
		obj.BackColor = ColorTranslator.FromHtml("#c66030");
		obj.ForeColor = ColorTranslator.FromHtml("#ffffff");
	}

	private void SureButton_MouseDown(object sender, MouseEventArgs e)
	{
		Button obj = sender as Button;
		obj.BackColor = ColorTranslator.FromHtml("#8a4322");
		obj.ForeColor = ColorTranslator.FromHtml("#ffffff");
	}

	private void NaticeWarningDialog_Load(object sender, EventArgs e)
	{
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		if (backgroundImage != null)
		{
			e.Graphics.DrawImage(backgroundImage, 0, 0, Width, Height);
		}
	}

	public static DialogResult ShowWarning(Form parent, string message, Action confirmAction)
	{
		using NaticeWarningDialog naticeWarningDialog = new NaticeWarningDialog(message, confirmAction);
		return naticeWarningDialog.ShowDialog(parent);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		base.SuspendLayout();
		base.AutoScaleDimensions = new System.Drawing.SizeF(11f, 24f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.Name = "NaticeWarningDialog";
		base.Padding = new System.Windows.Forms.Padding(24, 0, 24, 0);
		base.Size = new System.Drawing.Size(628, 348);
		base.Load += new System.EventHandler(this.NaticeWarningDialog_Load);
		base.ResumeLayout(false);
	}
}
