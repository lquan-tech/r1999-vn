using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Bluepoch.Common.Util;
using Bluepoch.Launcher.App;
using Bluepoch.Launcher.Config;
using Bluepoch.Launcher.Source.App;
using Bluepoch.Launcher.Source.Bean.Respond;
using Bluepoch.Launcher.Source.Utils;

namespace Bluepoch.Launcher.Source.Ui;

internal class NotifyForm : Form
{
	private Image backgroundImage;

	private Image iconImage;

	private static int FootHeight = 90;

	private static int Marge_Left = 30;

	private static int Marge_Left_offset = 12;

	private static int Marge_Top = 8;

	private static int Icon_Size = 64;

	private static int Title_Height = 42;

	private static int Title_Width = 260;

	private static int Message_Height = 48;

	private static int Message_Width = 320;

	private NotificationRespondInfo m_notificationRespondInfo;

	public NotifyForm(NotificationRespondInfo notificationRespondInfo)
	{
		NotifyForm notifyForm = this;
		SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint, value: true);
		FormBorderStyle = FormBorderStyle.None;
		StartPosition = FormStartPosition.Manual;
		TopMost = true;
		ShowInTaskbar = false;
		Width = 460;
		Height = 230;
		m_notificationRespondInfo = notificationRespondInfo;
		backgroundImage = Image.FromFile(NotificationPlugin.GetInstance().GetImageFilePathByUrl(notificationRespondInfo.bannerUrl));
		iconImage = Image.FromFile(LauncherUtils.GetAppIconFilePath());
		Label label = new Label
		{
			Text = notificationRespondInfo.title,
			Font = new Font(LauncherApplication.GetInstance().GetFontStyle(), 10.7f, FontStyle.Bold),
			ForeColor = Color.White,
			AutoSize = false,
			TextAlign = ContentAlignment.TopLeft,
			BackColor = Color.Transparent
		};
		BluepochLogger.InfoLog($"Text length : {label.Text.Length}");
		if (label.Text.Length > 35)
		{
			label.Size = new Size(Title_Width, Title_Height);
		}
		else
		{
			label.AutoSize = true;
		}
		Controls.Add(label);
		Label label2 = new Label
		{
			Text = notificationRespondInfo.content,
			Font = new Font(LauncherApplication.GetInstance().GetFontStyle(), 9.1f),
			ForeColor = Color.White,
			Size = new Size(Message_Width, Message_Height),
			TextAlign = ContentAlignment.TopLeft,
			BackColor = Color.Transparent
		};
		BluepochLogger.DebugLog($"title size : {label.Size.Height}");
		if (label.Size.Height == Title_Height)
		{
			int num = Height - FootHeight + Marge_Top;
			label.Location = new Point(Marge_Left + Icon_Size + Marge_Left_offset, Height - FootHeight + Marge_Top);
			label2.Location = new Point(Marge_Left + Icon_Size + Marge_Left_offset, num + label.Size.Height);
		}
		else
		{
			int num2 = Height - FootHeight + Marge_Top + label.Size.Height / 4;
			label.Location = new Point(Marge_Left + Icon_Size + Marge_Left_offset, num2);
			label2.Location = new Point(Marge_Left + Icon_Size + Marge_Left_offset, num2 + label.Size.Height + Marge_Top);
		}
		Controls.Add(label2);
		PictureBox closeButton = new PictureBox
		{
			BackColor = Color.Transparent,
			Size = new Size(18, 18),
			Location = new Point(Width - 40, Height - FootHeight + 8),
			TabStop = false,
			Image = Image.FromFile(Path.Combine(Application.StartupPath, "Resources", "btn_close_normal.png")),
			SizeMode = PictureBoxSizeMode.StretchImage
		};
		closeButton.MouseEnter += (object? s, EventArgs e) =>
		{
			notifyForm.BackColor = Color.Transparent;
			closeButton.Image = Image.FromFile(Path.Combine(Application.StartupPath, "Resources", "btn_close_hover.png"));
		};
		closeButton.MouseLeave += (object? s, EventArgs e) =>
		{
			closeButton.Image = Image.FromFile(Path.Combine(Application.StartupPath, "Resources", "btn_close_normal.png"));
		};
		closeButton.MouseDown += (object? s, MouseEventArgs e) =>
		{
			closeButton.Image = Image.FromFile(Path.Combine(Application.StartupPath, "Resources", "btn_close_press.png"));
		};
		closeButton.Click += (object? s, EventArgs e) =>
		{
			notifyForm.Close();
		};
		Controls.Add(closeButton);
		Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
		Location = new Point(workingArea.Width - Width, workingArea.Height - Height);
		label.Click += (object? s, EventArgs e) =>
		{
			BluepochLogger.DebugLog(" title.Click");
			notifyForm.labelClick(notificationRespondInfo.linkUrl);
		};
		label2.Click += (object? s, EventArgs e) =>
		{
			BluepochLogger.DebugLog(" messageLabel.Click");
			notifyForm.labelClick(notificationRespondInfo.linkUrl);
		};
	}

	private void labelClick(string url)
	{
		if (!string.IsNullOrEmpty(m_notificationRespondInfo?.linkUrl))
		{
			try
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = m_notificationRespondInfo?.linkUrl,
					UseShellExecute = true
				});
				return;
			}
			catch (Exception)
			{
				ActiviedMainWindow();
				return;
			}
		}
		ActiviedMainWindow();
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		base.OnMouseClick(e);
		BluepochLogger.DebugLog(" OnMouseClick.Click");
		ActiviedMainWindow();
	}

	private void ActiviedMainWindow()
	{
		if (Application.OpenForms[0].Name.Equals(SettingConfigManager.GetInstance().GetManifest()["app_displayname"]))
		{
			Application.OpenForms[0].WindowState = FormWindowState.Normal;
			Application.OpenForms[0].ShowInTaskbar = true;
			Application.OpenForms[0].Activate();
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		if (backgroundImage != null)
		{
			e.Graphics.DrawImage(backgroundImage, 0, 0, Width, Height);
		}
		using (SolidBrush brush = new SolidBrush(Color.FromArgb(132, 0, 0, 0)))
		{
			e.Graphics.FillRectangle(brush, 0, Height - FootHeight, Width, FootHeight);
		}
		if (iconImage != null)
		{
			e.Graphics.DrawImage(iconImage, Marge_Left, Height - FootHeight + (FootHeight - Icon_Size) / 2 + 2, Icon_Size, Icon_Size);
		}
	}
}
