namespace OnlineEgitim.UI
{
    partial class KurtarmaKoduForm
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
            this.lblAciklama = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.txtAd = new System.Windows.Forms.TextBox();
            this.btnUret = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.txtKod = new System.Windows.Forms.TextBox();
            this.lblBitis = new System.Windows.Forms.Label();
            this.btnKapat = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // lblAciklama
            //
            this.lblAciklama.Location = new System.Drawing.Point(13, 12);
            this.lblAciklama.Name = "lblAciklama";
            this.lblAciklama.Size = new System.Drawing.Size(392, 52);
            this.lblAciklama.TabIndex = 0;
            this.lblAciklama.Text = "Kendi dersinize kayıtlı, şifresi olmayan veya şifresini unutan bir öğrenci için tek kullanımlık kurtarma kodu üretin (24 saat geçerli). Kodu öğrenciye güvenli biçimde iletin; öğrenci kodla yeni şifresini belirler. Kod yalnızca burada bir kez gösterilir.";
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 75);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(98, 16);
            this.label1.TabIndex = 1;
            this.label1.Text = "Öğrencinin Adı";
            //
            // txtAd
            //
            this.txtAd.Location = new System.Drawing.Point(140, 72);
            this.txtAd.Name = "txtAd";
            this.txtAd.Size = new System.Drawing.Size(265, 22);
            this.txtAd.TabIndex = 2;
            //
            // btnUret
            //
            this.btnUret.Location = new System.Drawing.Point(285, 104);
            this.btnUret.Name = "btnUret";
            this.btnUret.Size = new System.Drawing.Size(120, 30);
            this.btnUret.TabIndex = 3;
            this.btnUret.Text = "Kod Üret";
            this.btnUret.UseVisualStyleBackColor = true;
            this.btnUret.Click += new System.EventHandler(this.btnUret_Click);
            //
            // label2
            //
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(13, 153);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(95, 16);
            this.label2.TabIndex = 4;
            this.label2.Text = "Kurtarma Kodu";
            //
            // txtKod
            //
            this.txtKod.Font = new System.Drawing.Font("Consolas", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.txtKod.Location = new System.Drawing.Point(140, 149);
            this.txtKod.Name = "txtKod";
            this.txtKod.ReadOnly = true;
            this.txtKod.Size = new System.Drawing.Size(265, 25);
            this.txtKod.TabIndex = 5;
            //
            // lblBitis
            //
            this.lblBitis.Location = new System.Drawing.Point(137, 180);
            this.lblBitis.Name = "lblBitis";
            this.lblBitis.Size = new System.Drawing.Size(268, 20);
            this.lblBitis.TabIndex = 6;
            //
            // btnKapat
            //
            this.btnKapat.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnKapat.Location = new System.Drawing.Point(311, 208);
            this.btnKapat.Name = "btnKapat";
            this.btnKapat.Size = new System.Drawing.Size(94, 30);
            this.btnKapat.TabIndex = 7;
            this.btnKapat.Text = "Kapat";
            this.btnKapat.UseVisualStyleBackColor = true;
            //
            // KurtarmaKoduForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(420, 252);
            this.Controls.Add(this.btnKapat);
            this.Controls.Add(this.lblBitis);
            this.Controls.Add(this.txtKod);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.btnUret);
            this.Controls.Add(this.txtAd);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.lblAciklama);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "KurtarmaKoduForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Kurtarma Kodu";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblAciklama;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtAd;
        private System.Windows.Forms.Button btnUret;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txtKod;
        private System.Windows.Forms.Label lblBitis;
        private System.Windows.Forms.Button btnKapat;
    }
}
