namespace OnlineEgitim.UI
{
    partial class SifreDegistirForm
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
            this.lblAciklama = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.txtMevcut = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.txtYeni = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.txtYeniTekrar = new System.Windows.Forms.TextBox();
            this.btnDegistir = new System.Windows.Forms.Button();
            this.btnIptal = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // lblAciklama
            //
            this.lblAciklama.Location = new System.Drawing.Point(16, 12);
            this.lblAciklama.Name = "lblAciklama";
            this.lblAciklama.Size = new System.Drawing.Size(392, 52);
            this.lblAciklama.TabIndex = 0;
            this.lblAciklama.Text = "Şifreniz değiştirildiğinde bu ve diğer cihazlardaki tüm oturumlarınız kapanır; yeni şifrenizle yeniden giriş yaparsınız.";
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(16, 74);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(82, 16);
            this.label1.TabIndex = 1;
            this.label1.Text = "Mevcut Şifre";
            //
            // txtMevcut
            //
            this.txtMevcut.Location = new System.Drawing.Point(150, 71);
            this.txtMevcut.Name = "txtMevcut";
            this.txtMevcut.Size = new System.Drawing.Size(258, 22);
            this.txtMevcut.TabIndex = 2;
            this.txtMevcut.UseSystemPasswordChar = true;
            //
            // label2
            //
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(16, 106);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(67, 16);
            this.label2.TabIndex = 3;
            this.label2.Text = "Yeni Şifre";
            //
            // txtYeni
            //
            this.txtYeni.Location = new System.Drawing.Point(150, 103);
            this.txtYeni.Name = "txtYeni";
            this.txtYeni.Size = new System.Drawing.Size(258, 22);
            this.txtYeni.TabIndex = 4;
            this.txtYeni.UseSystemPasswordChar = true;
            //
            // label3
            //
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(16, 138);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(118, 16);
            this.label3.TabIndex = 5;
            this.label3.Text = "Yeni Şifre (Tekrar)";
            //
            // txtYeniTekrar
            //
            this.txtYeniTekrar.Location = new System.Drawing.Point(150, 135);
            this.txtYeniTekrar.Name = "txtYeniTekrar";
            this.txtYeniTekrar.Size = new System.Drawing.Size(258, 22);
            this.txtYeniTekrar.TabIndex = 6;
            this.txtYeniTekrar.UseSystemPasswordChar = true;
            //
            // btnDegistir
            //
            this.btnDegistir.Location = new System.Drawing.Point(150, 174);
            this.btnDegistir.Name = "btnDegistir";
            this.btnDegistir.Size = new System.Drawing.Size(140, 30);
            this.btnDegistir.TabIndex = 7;
            this.btnDegistir.Text = "Şifreyi Değiştir";
            this.btnDegistir.UseVisualStyleBackColor = true;
            this.btnDegistir.Click += new System.EventHandler(this.btnDegistir_Click);
            //
            // btnIptal
            //
            this.btnIptal.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnIptal.Location = new System.Drawing.Point(300, 174);
            this.btnIptal.Name = "btnIptal";
            this.btnIptal.Size = new System.Drawing.Size(108, 30);
            this.btnIptal.TabIndex = 8;
            this.btnIptal.Text = "İptal";
            this.btnIptal.UseVisualStyleBackColor = true;
            //
            // SifreDegistirForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(424, 220);
            this.Controls.Add(this.btnIptal);
            this.Controls.Add(this.btnDegistir);
            this.Controls.Add(this.txtYeniTekrar);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.txtYeni);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.txtMevcut);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.lblAciklama);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SifreDegistirForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Şifremi Değiştir";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblAciklama;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtMevcut;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txtYeni;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox txtYeniTekrar;
        private System.Windows.Forms.Button btnDegistir;
        private System.Windows.Forms.Button btnIptal;
    }
}
