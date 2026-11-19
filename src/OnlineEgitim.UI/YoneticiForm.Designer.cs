namespace OnlineEgitim.UI
{
    partial class YoneticiForm
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
            this.lblBaslik = new System.Windows.Forms.Label();
            this.lblAra = new System.Windows.Forms.Label();
            this.txtAra = new System.Windows.Forms.TextBox();
            this.lblRol = new System.Windows.Forms.Label();
            this.cmbRol = new System.Windows.Forms.ComboBox();
            this.btnAra = new System.Windows.Forms.Button();
            this.lstHesaplar = new System.Windows.Forms.ListBox();
            this.btnOnceki = new System.Windows.Forms.Button();
            this.lblSayfa = new System.Windows.Forms.Label();
            this.btnSonraki = new System.Windows.Forms.Button();
            this.btnDevreDisi = new System.Windows.Forms.Button();
            this.btnEtkinlestir = new System.Windows.Forms.Button();
            this.btnYoneticilikten = new System.Windows.Forms.Button();
            this.btnCikis = new System.Windows.Forms.Button();
            this.grpYeni = new System.Windows.Forms.GroupBox();
            this.rbOgretmen = new System.Windows.Forms.RadioButton();
            this.rbOgrenci = new System.Windows.Forms.RadioButton();
            this.lblAd = new System.Windows.Forms.Label();
            this.txtAd = new System.Windows.Forms.TextBox();
            this.lblDetay = new System.Windows.Forms.Label();
            this.txtDetay = new System.Windows.Forms.TextBox();
            this.btnOlustur = new System.Windows.Forms.Button();
            this.lblKodBaslik = new System.Windows.Forms.Label();
            this.txtKod = new System.Windows.Forms.TextBox();
            this.lblKodBitis = new System.Windows.Forms.Label();
            this.grpYeni.SuspendLayout();
            this.btnSifirlamaKodu = new System.Windows.Forms.Button();
            this.btnSifremiDegistir = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // lblBaslik
            //
            this.lblBaslik.AutoSize = true;
            this.lblBaslik.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.lblBaslik.Location = new System.Drawing.Point(16, 11);
            this.lblBaslik.Name = "lblBaslik";
            this.lblBaslik.Size = new System.Drawing.Size(140, 20);
            this.lblBaslik.TabIndex = 0;
            this.lblBaslik.Text = "Kullanıcı Yönetimi";
            //
            // lblAra
            //
            this.lblAra.AutoSize = true;
            this.lblAra.Location = new System.Drawing.Point(16, 47);
            this.lblAra.Name = "lblAra";
            this.lblAra.Size = new System.Drawing.Size(27, 16);
            this.lblAra.TabIndex = 1;
            this.lblAra.Text = "Ara";
            //
            // txtAra
            //
            this.txtAra.Location = new System.Drawing.Point(60, 44);
            this.txtAra.Name = "txtAra";
            this.txtAra.Size = new System.Drawing.Size(250, 22);
            this.txtAra.TabIndex = 2;
            //
            // lblRol
            //
            this.lblRol.AutoSize = true;
            this.lblRol.Location = new System.Drawing.Point(322, 47);
            this.lblRol.Name = "lblRol";
            this.lblRol.Size = new System.Drawing.Size(27, 16);
            this.lblRol.TabIndex = 3;
            this.lblRol.Text = "Rol";
            //
            // cmbRol
            //
            this.cmbRol.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbRol.FormattingEnabled = true;
            this.cmbRol.Location = new System.Drawing.Point(356, 43);
            this.cmbRol.Name = "cmbRol";
            this.cmbRol.Size = new System.Drawing.Size(114, 24);
            this.cmbRol.TabIndex = 4;
            this.cmbRol.SelectedIndexChanged += new System.EventHandler(this.cmbRol_SelectedIndexChanged);
            //
            // btnAra
            //
            this.btnAra.Location = new System.Drawing.Point(480, 40);
            this.btnAra.Name = "btnAra";
            this.btnAra.Size = new System.Drawing.Size(80, 30);
            this.btnAra.TabIndex = 5;
            this.btnAra.Text = "Ara";
            this.btnAra.UseVisualStyleBackColor = true;
            this.btnAra.Click += new System.EventHandler(this.btnAra_Click);
            //
            // lstHesaplar
            //
            this.lstHesaplar.FormattingEnabled = true;
            this.lstHesaplar.HorizontalScrollbar = true;
            this.lstHesaplar.ItemHeight = 16;
            this.lstHesaplar.Location = new System.Drawing.Point(16, 80);
            this.lstHesaplar.Name = "lstHesaplar";
            this.lstHesaplar.Size = new System.Drawing.Size(544, 292);
            this.lstHesaplar.TabIndex = 6;
            //
            // btnOnceki
            //
            this.btnOnceki.Enabled = false;
            this.btnOnceki.Location = new System.Drawing.Point(16, 384);
            this.btnOnceki.Name = "btnOnceki";
            this.btnOnceki.Size = new System.Drawing.Size(100, 30);
            this.btnOnceki.TabIndex = 7;
            this.btnOnceki.Text = "< Önceki";
            this.btnOnceki.UseVisualStyleBackColor = true;
            this.btnOnceki.Click += new System.EventHandler(this.btnOnceki_Click);
            //
            // lblSayfa
            //
            this.lblSayfa.Location = new System.Drawing.Point(122, 384);
            this.lblSayfa.Name = "lblSayfa";
            this.lblSayfa.Size = new System.Drawing.Size(332, 30);
            this.lblSayfa.TabIndex = 8;
            this.lblSayfa.Text = "-";
            this.lblSayfa.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // btnSonraki
            //
            this.btnSonraki.Enabled = false;
            this.btnSonraki.Location = new System.Drawing.Point(460, 384);
            this.btnSonraki.Name = "btnSonraki";
            this.btnSonraki.Size = new System.Drawing.Size(100, 30);
            this.btnSonraki.TabIndex = 9;
            this.btnSonraki.Text = "Sonraki >";
            this.btnSonraki.UseVisualStyleBackColor = true;
            this.btnSonraki.Click += new System.EventHandler(this.btnSonraki_Click);
            //
            // btnDevreDisi
            //
            this.btnDevreDisi.Location = new System.Drawing.Point(16, 428);
            this.btnDevreDisi.Name = "btnDevreDisi";
            this.btnDevreDisi.Size = new System.Drawing.Size(176, 34);
            this.btnDevreDisi.TabIndex = 10;
            this.btnDevreDisi.Text = "Devre Dışı Bırak";
            this.btnDevreDisi.UseVisualStyleBackColor = true;
            this.btnDevreDisi.Click += new System.EventHandler(this.btnDevreDisi_Click);
            //
            // btnEtkinlestir
            //
            this.btnEtkinlestir.Location = new System.Drawing.Point(200, 428);
            this.btnEtkinlestir.Name = "btnEtkinlestir";
            this.btnEtkinlestir.Size = new System.Drawing.Size(176, 34);
            this.btnEtkinlestir.TabIndex = 11;
            this.btnEtkinlestir.Text = "Etkinleştir";
            this.btnEtkinlestir.UseVisualStyleBackColor = true;
            this.btnEtkinlestir.Click += new System.EventHandler(this.btnEtkinlestir_Click);
            //
            // btnYoneticilikten
            //
            this.btnYoneticilikten.Location = new System.Drawing.Point(384, 428);
            this.btnYoneticilikten.Name = "btnYoneticilikten";
            this.btnYoneticilikten.Size = new System.Drawing.Size(176, 34);
            this.btnYoneticilikten.TabIndex = 12;
            this.btnYoneticilikten.Text = "Yöneticilikten Çıkar";
            this.btnYoneticilikten.UseVisualStyleBackColor = true;
            this.btnYoneticilikten.Click += new System.EventHandler(this.btnYoneticilikten_Click);
            //
            // btnCikis
            //
            this.btnCikis.Location = new System.Drawing.Point(384, 470);
            this.btnCikis.Name = "btnCikis";
            this.btnCikis.Size = new System.Drawing.Size(176, 34);
            this.btnCikis.TabIndex = 15;
            this.btnCikis.Text = "Çıkış Yap";
            this.btnCikis.UseVisualStyleBackColor = true;
            this.btnCikis.Click += new System.EventHandler(this.btnCikis_Click);
            //
            // grpYeni
            //
            this.grpYeni.Controls.Add(this.lblKodBitis);
            this.grpYeni.Controls.Add(this.txtKod);
            this.grpYeni.Controls.Add(this.lblKodBaslik);
            this.grpYeni.Controls.Add(this.btnOlustur);
            this.grpYeni.Controls.Add(this.txtDetay);
            this.grpYeni.Controls.Add(this.lblDetay);
            this.grpYeni.Controls.Add(this.txtAd);
            this.grpYeni.Controls.Add(this.lblAd);
            this.grpYeni.Controls.Add(this.rbOgrenci);
            this.grpYeni.Controls.Add(this.rbOgretmen);
            this.grpYeni.Location = new System.Drawing.Point(580, 40);
            this.grpYeni.Name = "grpYeni";
            this.grpYeni.Size = new System.Drawing.Size(324, 374);
            this.grpYeni.TabIndex = 16;
            this.grpYeni.TabStop = false;
            this.grpYeni.Text = "Yeni Hesap";
            //
            // rbOgretmen
            //
            this.rbOgretmen.AutoSize = true;
            this.rbOgretmen.Checked = true;
            this.rbOgretmen.Location = new System.Drawing.Point(15, 28);
            this.rbOgretmen.Name = "rbOgretmen";
            this.rbOgretmen.Size = new System.Drawing.Size(85, 20);
            this.rbOgretmen.TabIndex = 0;
            this.rbOgretmen.TabStop = true;
            this.rbOgretmen.Text = "Öğretmen";
            this.rbOgretmen.UseVisualStyleBackColor = true;
            this.rbOgretmen.CheckedChanged += new System.EventHandler(this.rbTur_CheckedChanged);
            //
            // rbOgrenci
            //
            this.rbOgrenci.AutoSize = true;
            this.rbOgrenci.Location = new System.Drawing.Point(130, 28);
            this.rbOgrenci.Name = "rbOgrenci";
            this.rbOgrenci.Size = new System.Drawing.Size(73, 20);
            this.rbOgrenci.TabIndex = 1;
            this.rbOgrenci.Text = "Öğrenci";
            this.rbOgrenci.UseVisualStyleBackColor = true;
            this.rbOgrenci.CheckedChanged += new System.EventHandler(this.rbTur_CheckedChanged);
            //
            // lblAd
            //
            this.lblAd.AutoSize = true;
            this.lblAd.Location = new System.Drawing.Point(15, 62);
            this.lblAd.Name = "lblAd";
            this.lblAd.Size = new System.Drawing.Size(84, 16);
            this.lblAd.TabIndex = 2;
            this.lblAd.Text = "İsim Soyisim";
            //
            // txtAd
            //
            this.txtAd.Location = new System.Drawing.Point(15, 82);
            this.txtAd.Name = "txtAd";
            this.txtAd.Size = new System.Drawing.Size(294, 22);
            this.txtAd.TabIndex = 3;
            //
            // lblDetay
            //
            this.lblDetay.AutoSize = true;
            this.lblDetay.Location = new System.Drawing.Point(15, 114);
            this.lblDetay.Name = "lblDetay";
            this.lblDetay.Size = new System.Drawing.Size(95, 16);
            this.lblDetay.TabIndex = 4;
            this.lblDetay.Text = "Uzmanlık Alanı";
            //
            // txtDetay
            //
            this.txtDetay.Location = new System.Drawing.Point(15, 134);
            this.txtDetay.Name = "txtDetay";
            this.txtDetay.Size = new System.Drawing.Size(294, 22);
            this.txtDetay.TabIndex = 5;
            //
            // btnOlustur
            //
            this.btnOlustur.Location = new System.Drawing.Point(15, 170);
            this.btnOlustur.Name = "btnOlustur";
            this.btnOlustur.Size = new System.Drawing.Size(150, 32);
            this.btnOlustur.TabIndex = 6;
            this.btnOlustur.Text = "Hesabı Oluştur";
            this.btnOlustur.UseVisualStyleBackColor = true;
            this.btnOlustur.Click += new System.EventHandler(this.btnOlustur_Click);
            //
            // lblKodBaslik
            //
            this.lblKodBaslik.Location = new System.Drawing.Point(15, 218);
            this.lblKodBaslik.Name = "lblKodBaslik";
            this.lblKodBaslik.Size = new System.Drawing.Size(294, 40);
            this.lblKodBaslik.TabIndex = 7;
            this.lblKodBaslik.Text = "Tek kullanımlık kod (yalnızca bir kez gösterilir; kişiye güvenli bir yolla iletin):";
            //
            // txtKod
            //
            this.txtKod.Location = new System.Drawing.Point(15, 262);
            this.txtKod.Name = "txtKod";
            this.txtKod.ReadOnly = true;
            this.txtKod.Size = new System.Drawing.Size(294, 22);
            this.txtKod.TabIndex = 8;
            this.txtKod.TabStop = false;
            //
            // lblKodBitis
            //
            this.lblKodBitis.Location = new System.Drawing.Point(15, 292);
            this.lblKodBitis.Name = "lblKodBitis";
            this.lblKodBitis.Size = new System.Drawing.Size(294, 70);
            this.lblKodBitis.TabIndex = 9;
            // 
            // btnSifremiDegistir
            // 
            this.btnSifremiDegistir.Location = new System.Drawing.Point(200, 470);
            this.btnSifremiDegistir.Name = "btnSifremiDegistir";
            this.btnSifremiDegistir.Size = new System.Drawing.Size(176, 34);
            this.btnSifremiDegistir.TabIndex = 14;
            this.btnSifremiDegistir.Text = "Şifremi Değiştir";
            this.btnSifremiDegistir.UseVisualStyleBackColor = true;
            this.btnSifremiDegistir.Click += new System.EventHandler(this.btnSifremiDegistir_Click);
            // 
            // btnSifirlamaKodu
            // 
            this.btnSifirlamaKodu.Location = new System.Drawing.Point(16, 470);
            this.btnSifirlamaKodu.Name = "btnSifirlamaKodu";
            this.btnSifirlamaKodu.Size = new System.Drawing.Size(176, 34);
            this.btnSifirlamaKodu.TabIndex = 13;
            this.btnSifirlamaKodu.Text = "Sıfırlama Kodu Üret";
            this.btnSifirlamaKodu.UseVisualStyleBackColor = true;
            this.btnSifirlamaKodu.Click += new System.EventHandler(this.btnSifirlamaKodu_Click);
            //
            // YoneticiForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.ClientSize = new System.Drawing.Size(920, 520);
            this.Controls.Add(this.btnSifremiDegistir);
            this.Controls.Add(this.btnSifirlamaKodu);
            this.Controls.Add(this.grpYeni);
            this.Controls.Add(this.btnCikis);
            this.Controls.Add(this.btnYoneticilikten);
            this.Controls.Add(this.btnEtkinlestir);
            this.Controls.Add(this.btnDevreDisi);
            this.Controls.Add(this.btnSonraki);
            this.Controls.Add(this.lblSayfa);
            this.Controls.Add(this.btnOnceki);
            this.Controls.Add(this.lstHesaplar);
            this.Controls.Add(this.btnAra);
            this.Controls.Add(this.cmbRol);
            this.Controls.Add(this.lblRol);
            this.Controls.Add(this.txtAra);
            this.Controls.Add(this.lblAra);
            this.Controls.Add(this.lblBaslik);
            this.Name = "YoneticiForm";
            this.Text = "Yönetici Paneli";
            this.grpYeni.ResumeLayout(false);
            this.grpYeni.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblBaslik;
        private System.Windows.Forms.Label lblAra;
        private System.Windows.Forms.TextBox txtAra;
        private System.Windows.Forms.Label lblRol;
        private System.Windows.Forms.ComboBox cmbRol;
        private System.Windows.Forms.Button btnAra;
        private System.Windows.Forms.ListBox lstHesaplar;
        private System.Windows.Forms.Button btnOnceki;
        private System.Windows.Forms.Label lblSayfa;
        private System.Windows.Forms.Button btnSonraki;
        private System.Windows.Forms.Button btnDevreDisi;
        private System.Windows.Forms.Button btnEtkinlestir;
        private System.Windows.Forms.Button btnYoneticilikten;
        private System.Windows.Forms.Button btnCikis;
        private System.Windows.Forms.Button btnSifremiDegistir;
        private System.Windows.Forms.Button btnSifirlamaKodu;
        private System.Windows.Forms.GroupBox grpYeni;
        private System.Windows.Forms.RadioButton rbOgretmen;
        private System.Windows.Forms.RadioButton rbOgrenci;
        private System.Windows.Forms.Label lblAd;
        private System.Windows.Forms.TextBox txtAd;
        private System.Windows.Forms.Label lblDetay;
        private System.Windows.Forms.TextBox txtDetay;
        private System.Windows.Forms.Button btnOlustur;
        private System.Windows.Forms.Label lblKodBaslik;
        private System.Windows.Forms.TextBox txtKod;
        private System.Windows.Forms.Label lblKodBitis;
    }
}
