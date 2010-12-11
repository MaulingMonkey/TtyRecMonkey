namespace TtyRecMonkey {
	partial class FileOrderingForm {
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose( bool disposing ) {
			if (disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			this.labelDelay = new System.Windows.Forms.Label();
			this.tbDelay = new System.Windows.Forms.TextBox();
			this.rbOrderByCreation = new System.Windows.Forms.RadioButton();
			this.rbOrderByModification = new System.Windows.Forms.RadioButton();
			this.rbOrderByName = new System.Windows.Forms.RadioButton();
			this.lbFiles = new System.Windows.Forms.ListBox();
			this.buttonPlay = new System.Windows.Forms.Button();
			this.buttonMoveFileUp = new System.Windows.Forms.Button();
			this.buttonMoveFileDown = new System.Windows.Forms.Button();
			this.SuspendLayout();
			// 
			// labelDelay
			// 
			this.labelDelay.AutoSize = true;
			this.labelDelay.Location = new System.Drawing.Point(9, 15);
			this.labelDelay.Name = "labelDelay";
			this.labelDelay.Size = new System.Drawing.Size(164, 13);
			this.labelDelay.TabIndex = 0;
			this.labelDelay.Text = "Delay between ttyrecs (seconds):";
			// 
			// tbDelay
			// 
			this.tbDelay.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.tbDelay.Location = new System.Drawing.Point(172, 12);
			this.tbDelay.Name = "tbDelay";
			this.tbDelay.Size = new System.Drawing.Size(100, 20);
			this.tbDelay.TabIndex = 1;
			this.tbDelay.Text = "5";
			this.tbDelay.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
			// 
			// rbOrderByCreation
			// 
			this.rbOrderByCreation.AutoSize = true;
			this.rbOrderByCreation.Location = new System.Drawing.Point(12, 38);
			this.rbOrderByCreation.Name = "rbOrderByCreation";
			this.rbOrderByCreation.Size = new System.Drawing.Size(144, 17);
			this.rbOrderByCreation.TabIndex = 2;
			this.rbOrderByCreation.TabStop = true;
			this.rbOrderByCreation.Text = "Order by file creation time";
			this.rbOrderByCreation.UseVisualStyleBackColor = true;
			this.rbOrderByCreation.CheckedChanged += new System.EventHandler(this.rbOrderByCreation_CheckedChanged);
			// 
			// rbOrderByModification
			// 
			this.rbOrderByModification.AutoSize = true;
			this.rbOrderByModification.Location = new System.Drawing.Point(12, 61);
			this.rbOrderByModification.Name = "rbOrderByModification";
			this.rbOrderByModification.Size = new System.Drawing.Size(162, 17);
			this.rbOrderByModification.TabIndex = 3;
			this.rbOrderByModification.TabStop = true;
			this.rbOrderByModification.Text = "Order by file modification time";
			this.rbOrderByModification.UseVisualStyleBackColor = true;
			this.rbOrderByModification.CheckedChanged += new System.EventHandler(this.rbOrderByModification_CheckedChanged);
			// 
			// rbOrderByName
			// 
			this.rbOrderByName.AutoSize = true;
			this.rbOrderByName.Location = new System.Drawing.Point(12, 84);
			this.rbOrderByName.Name = "rbOrderByName";
			this.rbOrderByName.Size = new System.Drawing.Size(110, 17);
			this.rbOrderByName.TabIndex = 4;
			this.rbOrderByName.TabStop = true;
			this.rbOrderByName.Text = "Order by file name";
			this.rbOrderByName.UseVisualStyleBackColor = true;
			this.rbOrderByName.CheckedChanged += new System.EventHandler(this.rbOrderByName_CheckedChanged);
			// 
			// lbFiles
			// 
			this.lbFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
						| System.Windows.Forms.AnchorStyles.Left)
						| System.Windows.Forms.AnchorStyles.Right)));
			this.lbFiles.FormattingEnabled = true;
			this.lbFiles.Location = new System.Drawing.Point(12, 107);
			this.lbFiles.Name = "lbFiles";
			this.lbFiles.Size = new System.Drawing.Size(260, 95);
			this.lbFiles.TabIndex = 5;
			// 
			// buttonPlay
			// 
			this.buttonPlay.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.buttonPlay.Location = new System.Drawing.Point(12, 208);
			this.buttonPlay.Name = "buttonPlay";
			this.buttonPlay.Size = new System.Drawing.Size(75, 23);
			this.buttonPlay.TabIndex = 6;
			this.buttonPlay.Text = "Play";
			this.buttonPlay.UseVisualStyleBackColor = true;
			this.buttonPlay.Click += new System.EventHandler(this.buttonPlay_Click);
			// 
			// buttonMoveFileUp
			// 
			this.buttonMoveFileUp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.buttonMoveFileUp.Location = new System.Drawing.Point(116, 208);
			this.buttonMoveFileUp.Name = "buttonMoveFileUp";
			this.buttonMoveFileUp.Size = new System.Drawing.Size(75, 23);
			this.buttonMoveFileUp.TabIndex = 7;
			this.buttonMoveFileUp.Text = "Move Up";
			this.buttonMoveFileUp.UseVisualStyleBackColor = true;
			this.buttonMoveFileUp.Click += new System.EventHandler(this.buttonMoveFileUp_Click);
			// 
			// buttonMoveFileDown
			// 
			this.buttonMoveFileDown.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.buttonMoveFileDown.Location = new System.Drawing.Point(197, 208);
			this.buttonMoveFileDown.Name = "buttonMoveFileDown";
			this.buttonMoveFileDown.Size = new System.Drawing.Size(75, 23);
			this.buttonMoveFileDown.TabIndex = 8;
			this.buttonMoveFileDown.Text = "Move Down";
			this.buttonMoveFileDown.UseVisualStyleBackColor = true;
			this.buttonMoveFileDown.Click += new System.EventHandler(this.buttonMoveFileDown_Click);
			// 
			// FileOrderingForm
			// 
			this.AcceptButton = this.buttonPlay;
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(284, 243);
			this.Controls.Add(this.buttonMoveFileDown);
			this.Controls.Add(this.buttonMoveFileUp);
			this.Controls.Add(this.buttonPlay);
			this.Controls.Add(this.lbFiles);
			this.Controls.Add(this.rbOrderByName);
			this.Controls.Add(this.rbOrderByModification);
			this.Controls.Add(this.rbOrderByCreation);
			this.Controls.Add(this.tbDelay);
			this.Controls.Add(this.labelDelay);
			this.Name = "FileOrderingForm";
			this.Text = "Select file order...";
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label labelDelay;
		private System.Windows.Forms.TextBox tbDelay;
		private System.Windows.Forms.RadioButton rbOrderByCreation;
		private System.Windows.Forms.RadioButton rbOrderByModification;
		private System.Windows.Forms.RadioButton rbOrderByName;
		private System.Windows.Forms.ListBox lbFiles;
		private System.Windows.Forms.Button buttonPlay;
		private System.Windows.Forms.Button buttonMoveFileUp;
		private System.Windows.Forms.Button buttonMoveFileDown;


	}
}