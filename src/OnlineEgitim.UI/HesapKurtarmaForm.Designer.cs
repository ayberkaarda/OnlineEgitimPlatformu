namespace OnlineEgitim.UI
{
    partial class HesapKurtarmaForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.lblBaslik = new System.Windows.Forms.Label();
            this.lblAciklama = new System.Windows.Forms.Label();
            this.rbOgrenci = new System.Windows.Forms.RadioButton();
            this.rbOgretmen = new System.Windows.Forms.RadioButton();
            this.label1 = new System.Windows.Forms.Label();
            this.txtAd = new System.Windows.Forms.TextBox();
            this.lblKod = new System.Windows.Forms.Label();
            this.txtKod = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.txtSifre = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.txtSifreTekrar = new System.Windows.Forms.TextBox();
            this.btnKurtar = new System.Windows.Forms.Button();
            this.btnIptal = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // lblBaslik
            //
            this.lblBaslik.AutoSize = true;
            this.lblBaslik.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.lblBaslik.Location = new System.Drawing.Point(12, 12);
            this.lblBaslik.Name = "lblBaslik";
            this.lblBaslik.Size = new System.Drawing.Size(222, 20);
            this.lblBaslik.TabIndex = 0;
            this.lblBaslik.Text = "Şifresiz Hesap Kurtarma";
            //
            // lblAciklama
            //
            this.lblAciklama.Location = new System.Drawing.Point(13, 42);
            this.lblAciklama.Name = "lblAciklama";
            this.lblAciklama.Size = new System.Drawing.Size(452, 80);
            this.lblAciklama.TabIndex = 1;
            //
            // rbOgrenci
            //
            this.rbOgrenci.AutoSize = true;
            this.rbOgrenci.Checked = true;
            this.rbOgrenci.Location = new System.Drawing.Point(16, 128);
            this.rbOgrenci.Name = "rbOgrenci";
            this.rbOgrenci.Size = new System.Drawing.Size(300, 20);
            this.rbOgrenci.TabIndex = 2;
            this.rbOgrenci.TabStop = true;
            this.rbOgrenci.Text = "Öğrenci (öğretmenin verdiği kurtarma kodu)";
            this.rbOgrenci.UseVisualStyleBackColor = true;
            this.rbOgrenci.CheckedChanged += new System.EventHandler(this.rbRol_CheckedChanged);
            //
            // rbOgretmen
            //
            this.rbOgretmen.AutoSize = true;
            this.rbOgretmen.Location = new System.Drawing.Point(16, 152);
            this.rbOgretmen.Name = "rbOgretmen";
            this.rbOgretmen.Size = new System.Drawing.Size(212, 20);
            this.rbOgretmen.TabIndex = 3;
            this.rbOgretmen.Text = "Öğretmen (kurulum anahtarı)";
            this.rbOgretmen.UseVisualStyleBackColor = true;
            this.rbOgretmen.CheckedChanged += new System.EventHandler(this.rbRol_CheckedChanged);
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 189);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(84, 16);
            this.label1.TabIndex = 4;
            this.label1.Text = "İsim Soyisim";
            //
            // txtAd
            //
            this.txtAd.Location = new System.Drawing.Point(160, 186);
            this.txtAd.Name = "txtAd";
            this.txtAd.Size = new System.Drawing.Size(305, 22);
            this.txtAd.TabIndex = 5;
            //
            // lblKod
            //
            this.lblKod.AutoSize = true;
            this.lblKod.Location = new System.Drawing.Point(13, 221);
            this.lblKod.Name = "lblKod";
            this.lblKod.Size = new System.Drawing.Size(95, 16);
            this.lblKod.TabIndex = 6;
            this.lblKod.Text = "Kurtarma Kodu";
            //
            // txtKod
            //
            this.txtKod.Location = new System.Drawing.Point(160, 218);
            this.txtKod.Name = "txtKod";
            this.txtKod.Size = new System.Drawing.Size(305, 22);
            this.txtKod.TabIndex = 7;
            this.txtKod.UseSystemPasswordChar = true;
            //
            // label3
            //
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(13, 253);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(67, 16);
            this.label3.TabIndex = 8;
            this.label3.Text = "Yeni Şifre";
            //
            // txtSifre
            //
            this.txtSifre.Location = new System.Drawing.Point(160, 250);
            this.txtSifre.Name = "txtSifre";
            this.txtSifre.Size = new System.Drawing.Size(305, 22);
            this.txtSifre.TabIndex = 9;
            this.txtSifre.UseSystemPasswordChar = true;
            //
            // label4
            //
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(13, 285);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(118, 16);
            this.label4.TabIndex = 10;
            this.label4.Text = "Yeni Şifre (Tekrar)";
            //
            // txtSifreTekrar
            //
            this.txtSifreTekrar.Location = new System.Drawing.Point(160, 282);
            this.txtSifreTekrar.Name = "txtSifreTekrar";
            this.txtSifreTekrar.Size = new System.Drawing.Size(305, 22);
            this.txtSifreTekrar.TabIndex = 11;
            this.txtSifreTekrar.UseSystemPasswordChar = true;
            //
            // btnKurtar
            //
            this.btnKurtar.Location = new System.Drawing.Point(245, 322);
            this.btnKurtar.Name = "btnKurtar";
            this.btnKurtar.Size = new System.Drawing.Size(120, 30);
            this.btnKurtar.TabIndex = 12;
            this.btnKurtar.Text = "Şifreyi Belirle";
            this.btnKurtar.UseVisualStyleBackColor = true;
            this.btnKurtar.Click += new System.EventHandler(this.btnKurtar_Click);
            //
            // btnIptal
            //
            this.btnIptal.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnIptal.Location = new System.Drawing.Point(371, 322);
            this.btnIptal.Name = "btnIptal";
            this.btnIptal.Size = new System.Drawing.Size(94, 30);
            this.btnIptal.TabIndex = 13;
            this.btnIptal.Text = "İptal";
            this.btnIptal.UseVisualStyleBackColor = true;
            //
            // HesapKurtarmaForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(480, 366);
            this.Controls.Add(this.btnIptal);
            this.Controls.Add(this.btnKurtar);
            this.Controls.Add(this.txtSifreTekrar);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.txtSifre);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.txtKod);
            this.Controls.Add(this.lblKod);
            this.Controls.Add(this.txtAd);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.rbOgretmen);
            this.Controls.Add(this.rbOgrenci);
            this.Controls.Add(this.lblAciklama);
            this.Controls.Add(this.lblBaslik);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "HesapKurtarmaForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Hesap Kurtarma";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblBaslik;
        private System.Windows.Forms.Label lblAciklama;
        private System.Windows.Forms.RadioButton rbOgrenci;
        private System.Windows.Forms.RadioButton rbOgretmen;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtAd;
        private System.Windows.Forms.Label lblKod;
        private System.Windows.Forms.TextBox txtKod;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox txtSifre;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox txtSifreTekrar;
        private System.Windows.Forms.Button btnKurtar;
        private System.Windows.Forms.Button btnIptal;
    }
}
