namespace OnlineEgitim.UI
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.lblhosgeldin = new System.Windows.Forms.Label();
            this.lblAra = new System.Windows.Forms.Label();
            this.txtAra = new System.Windows.Forms.TextBox();
            this.lblKonu = new System.Windows.Forms.Label();
            this.txtKonu = new System.Windows.Forms.TextBox();
            this.lblOgretmen = new System.Windows.Forms.Label();
            this.txtOgretmen = new System.Windows.Forms.TextBox();
            this.chkBosKontenjan = new System.Windows.Forms.CheckBox();
            this.lblKayit = new System.Windows.Forms.Label();
            this.cmbKayit = new System.Windows.Forms.ComboBox();
            this.btnAra = new System.Windows.Forms.Button();
            this.btnTemizle = new System.Windows.Forms.Button();
            this.lstKurslar = new System.Windows.Forms.ListBox();
            this.btnOnceki = new System.Windows.Forms.Button();
            this.lblSayfa = new System.Windows.Forms.Label();
            this.btnSonraki = new System.Windows.Forms.Button();
            this.btnKursDetay = new System.Windows.Forms.Button();
            this.btnDersSec = new System.Windows.Forms.Button();
            this.btnAyril = new System.Windows.Forms.Button();
            this.btnKurslarim = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // lblhosgeldin
            //
            this.lblhosgeldin.AutoSize = true;
            this.lblhosgeldin.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.lblhosgeldin.Location = new System.Drawing.Point(16, 11);
            this.lblhosgeldin.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblhosgeldin.Name = "lblhosgeldin";
            this.lblhosgeldin.Size = new System.Drawing.Size(15, 20);
            this.lblhosgeldin.TabIndex = 0;
            this.lblhosgeldin.Text = "-";
            //
            // lblAra
            //
            this.lblAra.AutoSize = true;
            this.lblAra.Location = new System.Drawing.Point(16, 47);
            this.lblAra.Name = "lblAra";
            this.lblAra.Size = new System.Drawing.Size(58, 16);
            this.lblAra.TabIndex = 1;
            this.lblAra.Text = "Kurs adı";
            //
            // txtAra
            //
            this.txtAra.Location = new System.Drawing.Point(90, 44);
            this.txtAra.Name = "txtAra";
            this.txtAra.Size = new System.Drawing.Size(150, 22);
            this.txtAra.TabIndex = 2;
            //
            // lblKonu
            //
            this.lblKonu.AutoSize = true;
            this.lblKonu.Location = new System.Drawing.Point(255, 47);
            this.lblKonu.Name = "lblKonu";
            this.lblKonu.Size = new System.Drawing.Size(37, 16);
            this.lblKonu.TabIndex = 3;
            this.lblKonu.Text = "Konu";
            //
            // txtKonu
            //
            this.txtKonu.Location = new System.Drawing.Point(300, 44);
            this.txtKonu.Name = "txtKonu";
            this.txtKonu.Size = new System.Drawing.Size(150, 22);
            this.txtKonu.TabIndex = 4;
            //
            // lblOgretmen
            //
            this.lblOgretmen.AutoSize = true;
            this.lblOgretmen.Location = new System.Drawing.Point(465, 47);
            this.lblOgretmen.Name = "lblOgretmen";
            this.lblOgretmen.Size = new System.Drawing.Size(65, 16);
            this.lblOgretmen.TabIndex = 5;
            this.lblOgretmen.Text = "Öğretmen";
            //
            // txtOgretmen
            //
            this.txtOgretmen.Location = new System.Drawing.Point(540, 44);
            this.txtOgretmen.Name = "txtOgretmen";
            this.txtOgretmen.Size = new System.Drawing.Size(200, 22);
            this.txtOgretmen.TabIndex = 6;
            //
            // chkBosKontenjan
            //
            this.chkBosKontenjan.AutoSize = true;
            this.chkBosKontenjan.Location = new System.Drawing.Point(16, 80);
            this.chkBosKontenjan.Name = "chkBosKontenjan";
            this.chkBosKontenjan.Size = new System.Drawing.Size(179, 20);
            this.chkBosKontenjan.TabIndex = 7;
            this.chkBosKontenjan.Text = "Yalnız boş yeri olanlar";
            this.chkBosKontenjan.UseVisualStyleBackColor = true;
            this.chkBosKontenjan.CheckedChanged += new System.EventHandler(this.Filter_Changed);
            //
            // lblKayit
            //
            this.lblKayit.AutoSize = true;
            this.lblKayit.Location = new System.Drawing.Point(230, 81);
            this.lblKayit.Name = "lblKayit";
            this.lblKayit.Size = new System.Drawing.Size(38, 16);
            this.lblKayit.TabIndex = 8;
            this.lblKayit.Text = "Kayıt";
            //
            // cmbKayit
            //
            this.cmbKayit.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbKayit.FormattingEnabled = true;
            this.cmbKayit.Location = new System.Drawing.Point(275, 77);
            this.cmbKayit.Name = "cmbKayit";
            this.cmbKayit.Size = new System.Drawing.Size(190, 24);
            this.cmbKayit.TabIndex = 9;
            this.cmbKayit.SelectedIndexChanged += new System.EventHandler(this.Filter_Changed);
            //
            // btnAra
            //
            this.btnAra.Location = new System.Drawing.Point(530, 74);
            this.btnAra.Name = "btnAra";
            this.btnAra.Size = new System.Drawing.Size(100, 30);
            this.btnAra.TabIndex = 10;
            this.btnAra.Text = "Ara";
            this.btnAra.UseVisualStyleBackColor = true;
            this.btnAra.Click += new System.EventHandler(this.btnAra_Click);
            //
            // btnTemizle
            //
            this.btnTemizle.Location = new System.Drawing.Point(640, 74);
            this.btnTemizle.Name = "btnTemizle";
            this.btnTemizle.Size = new System.Drawing.Size(100, 30);
            this.btnTemizle.TabIndex = 11;
            this.btnTemizle.Text = "Temizle";
            this.btnTemizle.UseVisualStyleBackColor = true;
            this.btnTemizle.Click += new System.EventHandler(this.btnTemizle_Click);
            //
            // lstKurslar
            //
            this.lstKurslar.FormattingEnabled = true;
            this.lstKurslar.HorizontalScrollbar = true;
            this.lstKurslar.ItemHeight = 16;
            this.lstKurslar.Location = new System.Drawing.Point(16, 115);
            this.lstKurslar.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.lstKurslar.Name = "lstKurslar";
            this.lstKurslar.Size = new System.Drawing.Size(724, 244);
            this.lstKurslar.TabIndex = 12;
            //
            // btnOnceki
            //
            this.btnOnceki.Enabled = false;
            this.btnOnceki.Location = new System.Drawing.Point(16, 366);
            this.btnOnceki.Name = "btnOnceki";
            this.btnOnceki.Size = new System.Drawing.Size(110, 30);
            this.btnOnceki.TabIndex = 13;
            this.btnOnceki.Text = "< Önceki";
            this.btnOnceki.UseVisualStyleBackColor = true;
            this.btnOnceki.Click += new System.EventHandler(this.btnOnceki_Click);
            //
            // lblSayfa
            //
            this.lblSayfa.Location = new System.Drawing.Point(132, 366);
            this.lblSayfa.Name = "lblSayfa";
            this.lblSayfa.Size = new System.Drawing.Size(492, 30);
            this.lblSayfa.TabIndex = 14;
            this.lblSayfa.Text = "-";
            this.lblSayfa.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // btnSonraki
            //
            this.btnSonraki.Enabled = false;
            this.btnSonraki.Location = new System.Drawing.Point(630, 366);
            this.btnSonraki.Name = "btnSonraki";
            this.btnSonraki.Size = new System.Drawing.Size(110, 30);
            this.btnSonraki.TabIndex = 15;
            this.btnSonraki.Text = "Sonraki >";
            this.btnSonraki.UseVisualStyleBackColor = true;
            this.btnSonraki.Click += new System.EventHandler(this.btnSonraki_Click);
            //
            // btnKursDetay
            //
            this.btnKursDetay.Location = new System.Drawing.Point(16, 410);
            this.btnKursDetay.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnKursDetay.Name = "btnKursDetay";
            this.btnKursDetay.Size = new System.Drawing.Size(140, 37);
            this.btnKursDetay.TabIndex = 16;
            this.btnKursDetay.Text = "Kurs Detayları";
            this.btnKursDetay.UseVisualStyleBackColor = true;
            this.btnKursDetay.Click += new System.EventHandler(this.btnKursDetay_Click);
            //
            // btnDersSec
            //
            this.btnDersSec.Location = new System.Drawing.Point(166, 410);
            this.btnDersSec.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnDersSec.Name = "btnDersSec";
            this.btnDersSec.Size = new System.Drawing.Size(140, 37);
            this.btnDersSec.TabIndex = 17;
            this.btnDersSec.Text = "Ders Seç";
            this.btnDersSec.UseVisualStyleBackColor = true;
            this.btnDersSec.Click += new System.EventHandler(this.btnDersSec_Click);
            //
            // btnAyril
            //
            this.btnAyril.Location = new System.Drawing.Point(316, 410);
            this.btnAyril.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnAyril.Name = "btnAyril";
            this.btnAyril.Size = new System.Drawing.Size(140, 37);
            this.btnAyril.TabIndex = 18;
            this.btnAyril.Text = "Dersten Ayrıl";
            this.btnAyril.UseVisualStyleBackColor = true;
            this.btnAyril.Click += new System.EventHandler(this.btnAyril_Click);
            //
            // btnKurslarim
            //
            this.btnKurslarim.Location = new System.Drawing.Point(466, 410);
            this.btnKurslarim.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnKurslarim.Name = "btnKurslarim";
            this.btnKurslarim.Size = new System.Drawing.Size(140, 37);
            this.btnKurslarim.TabIndex = 19;
            this.btnKurslarim.Text = "Kurslarım";
            this.btnKurslarim.UseVisualStyleBackColor = true;
            this.btnKurslarim.Click += new System.EventHandler(this.btnKurslarim_Click);
            //
            // MainForm
            //
            this.AcceptButton = this.btnAra;
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.ClientSize = new System.Drawing.Size(760, 465);
            this.Controls.Add(this.btnKurslarim);
            this.Controls.Add(this.btnAyril);
            this.Controls.Add(this.btnDersSec);
            this.Controls.Add(this.btnKursDetay);
            this.Controls.Add(this.btnSonraki);
            this.Controls.Add(this.lblSayfa);
            this.Controls.Add(this.btnOnceki);
            this.Controls.Add(this.lstKurslar);
            this.Controls.Add(this.btnTemizle);
            this.Controls.Add(this.btnAra);
            this.Controls.Add(this.cmbKayit);
            this.Controls.Add(this.lblKayit);
            this.Controls.Add(this.chkBosKontenjan);
            this.Controls.Add(this.txtOgretmen);
            this.Controls.Add(this.lblOgretmen);
            this.Controls.Add(this.txtKonu);
            this.Controls.Add(this.lblKonu);
            this.Controls.Add(this.txtAra);
            this.Controls.Add(this.lblAra);
            this.Controls.Add(this.lblhosgeldin);
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.Name = "MainForm";
            this.Text = "Öğrenci Paneli";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblhosgeldin;
        private System.Windows.Forms.Label lblAra;
        private System.Windows.Forms.TextBox txtAra;
        private System.Windows.Forms.Label lblKonu;
        private System.Windows.Forms.TextBox txtKonu;
        private System.Windows.Forms.Label lblOgretmen;
        private System.Windows.Forms.TextBox txtOgretmen;
        private System.Windows.Forms.CheckBox chkBosKontenjan;
        private System.Windows.Forms.Label lblKayit;
        private System.Windows.Forms.ComboBox cmbKayit;
        private System.Windows.Forms.Button btnAra;
        private System.Windows.Forms.Button btnTemizle;
        private System.Windows.Forms.ListBox lstKurslar;
        private System.Windows.Forms.Button btnOnceki;
        private System.Windows.Forms.Label lblSayfa;
        private System.Windows.Forms.Button btnSonraki;
        private System.Windows.Forms.Button btnKursDetay;
        private System.Windows.Forms.Button btnDersSec;
        private System.Windows.Forms.Button btnAyril;
        private System.Windows.Forms.Button btnKurslarim;
    }
}
