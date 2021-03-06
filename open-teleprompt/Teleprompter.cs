﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Diagnostics;

namespace open_teleprompt
{
    public partial class Teleprompter : Form
    {
        int line_height, start_X;
        int img_height, hmax, stsize;
        int speed, pcnt, draw_persec, timer_helper;
        double img_current_Y, delta;
        long start_time;
        //bool running = false;
        string txt, line_end;
        List<string> splines = new List<string>();
        List<Color> splc = new List<Color>();
        Image img, status;
        Size scr;

        public Teleprompter()
        {
            InitializeComponent();
            scr = Screen.FromControl(this).Bounds.Size;
            this.Size = scr;
            teletimer.Interval = TeleSettings.DrawInterval;
            if (TeleSettings.UsingMono)
                line_end = "\n";
            else
                line_end = "\r\n";
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.MouseWheel += new MouseEventHandler(Teleprompter_MouseWheel);
        }

        public string TeleText
        {
            set { value += " " + line_end + " "; sptxt.Text = value; }
        }

        private void Teleprompter_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                this.Close();
            else if (e.KeyCode == Keys.Home)
                ResetState(0, 0);
            else if (e.KeyCode == Keys.End)
                ResetState(0, hmax);
        }

        void ProcessText()
        {
            List<reporter_bgcolor> rb = TeleSettings.reporter_bgcolor_array;
            string[] lines = sptxt.Lines;
            List<Color> lc = new List<Color>();
            for (int i = 0; i < lines.Length; i++)
            {
                lc.Add(TeleSettings.BackGroundColor);
                for (int j = 0; j < rb.Count; j++)
                {
                    if (lines[i].StartsWith(rb[j].prefix))
                        lc[i] = rb[j].color;
                    if (lines[i].Length == 0)
                        lines[i] = " "; // avoid new line detection error 
                }
            }
            sptxt.Lines = lines;
            txt = sptxt.Text;

            string ls;
            int length = txt.Length, pos = 0, line = 0;
            Point current, last = new Point(-1, -1);
            sptxt.Font = TeleSettings.TextFont;
            for (int i = 0; i < length; i++)
            {
                current = sptxt.GetPositionFromCharIndex(i);
                if (current.X < last.X) //new line detected
                {
                    ls = txt.Substring(pos, i - pos);
                    splines.Add(ls);
                    splc.Add(lc[line]);
                    if (ls.EndsWith(line_end))
                        line++;
                    pos = i;
                    line_height = current.Y - last.Y;
                    start_X = current.X;
                }
                last = current;
            }
            splines.Add(txt.Substring(pos, length - pos));
            splc.Add(lc[line]);
        }

        void DrawText()
        {
            int start_Y = scr.Height / 4 * 3, y = start_Y;
            img_height = start_Y + line_height * (splines.Count - 1) + start_Y;
            img = new Bitmap(scr.Width, img_height, PixelFormat.Format24bppRgb);
            Graphics g = Graphics.FromImage(img);
            g.FillRectangle(new SolidBrush(TeleSettings.BackGroundColor), 0, 0, img.Width, img.Height);
            for (int i = 0; i < splines.Count; i++)
            {
                g.FillRectangle(new SolidBrush(splc[i]), 0, y, img.Width, line_height);
                g.DrawString(splines[i], TeleSettings.TextFont, new SolidBrush(TeleSettings.TextColor), -TeleSettings.TextFont.Size / 4, y);
                y += line_height;
            }
            if (TeleSettings.TextFlip)
                img.RotateFlip(RotateFlipType.RotateNoneFlipX);
        }

        void SetParams()
        {
            img_current_Y = 0;
            draw_persec = 1000 / TeleSettings.DrawInterval;
            pcnt = draw_persec; // force draw in the first time
            start_time = 0;
            hmax = img_height - scr.Height;
            delta = (double)scr.Height / 20 / draw_persec; //one tenth of screen per second
        }

        void ResetState(int cspeed, int imgY)
        {
            speed = cspeed;
            img_current_Y = imgY;
        }

        public new DialogResult ShowDialog()
        {
            ProcessText();
            DrawText();
            SetParams();
            DrawStatus();

            this.Controls.Remove(sptxt);
            Invalidate();
            teletimer.Start();
            return base.ShowDialog();
        }

        void DrawStatus()
        {
            int h = scr.Height / 20 + 1; // use 1/20 height of screen to display status bar
            status = new Bitmap(scr.Width, h);
            Graphics g = Graphics.FromImage(status);
            int wpart = (int)((double)img_current_Y / hmax * status.Width);
            g.FillRectangle(new SolidBrush(Color.Purple), 0, 0, wpart, status.Height);
            g.FillRectangle(new SolidBrush(TeleSettings.BackGroundColor), wpart, 0, status.Width - wpart, status.Height);
            long time_elapsed = (start_time == 0 ? 0 : DateTime.Now.Ticks - start_time);
            DateTime dt = new DateTime(time_elapsed);
            StringBuilder s = new StringBuilder();
            s.Append("速度：").Append(speed).Append("   时间：").Append(new DateTime(time_elapsed).ToString("HH:mm:ss"));
            Font ori = TeleSettings.TextFont;
            if (stsize == 0)
            {
                int sizetmp = 200;
                while (sizetmp != stsize)
                {
                    int scur = (sizetmp + stsize) / 2;
                    int fh = new Font(ori.FontFamily, scur).Height;
                    if (fh > h)
                        sizetmp = scur;
                    else if (fh < h)
                        stsize = scur;
                    else
                        stsize = sizetmp = scur;
                    if (sizetmp - stsize <= 1)
                        stsize = sizetmp = scur;
                }
            }
            g.DrawString(s.ToString(), new Font(ori.FontFamily, stsize), new SolidBrush(TeleSettings.TextColor), start_X, 3);
            if (TeleSettings.TextFlip)
                status.RotateFlip(RotateFlipType.RotateNoneFlipX);
        }

        private void Teleprompter_Paint(object sender, PaintEventArgs e)
        {
            if (img_current_Y > hmax)
                ResetState(0, hmax);
            else if (img_current_Y < 0)
                ResetState(0, 0);

            //Stopwatch sw = new Stopwatch();
            //sw.Start();
            e.Graphics.DrawImage(img, 0, (int)-img_current_Y); // takes 7ms
            //sw.Stop();
            //MessageBox.Show(sw.ElapsedMilliseconds.ToString());
            if (TeleSettings.ShowStatus)
            {
                pcnt++;
                if (pcnt > draw_persec / 5)
                {
                    pcnt = 0;
                    DrawStatus();
                }
                e.Graphics.DrawImage(status, 0, 0);
            }
        }

        private void teletimer_Tick(object sender, EventArgs e)
        {
            if (speed != 0)
            {
                if (start_time == 0) start_time = DateTime.Now.Ticks;
                img_current_Y += speed * delta;
                Invalidate();
            }
            else
            {
                timer_helper++;
                if (timer_helper > draw_persec / 10)
                {
                    timer_helper = 0;
                    pcnt = draw_persec;
                    Invalidate();
                }
            }
        }

        void Teleprompter_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                speed++;
            else if (e.Button == MouseButtons.Right)
                speed--;
        }

        void Teleprompter_MouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta < 0)
                speed++;
            else
                speed--;
        }

        private void Teleprompter_FormClosed(object sender, FormClosedEventArgs e)
        {
            img.Dispose();
            status.Dispose();
        }
    }
}
