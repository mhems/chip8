namespace Chip8Gui
{
    partial class Chip8Gui
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            executeButton = new Button();
            SuspendLayout();
            // 
            // executeButton
            // 
            executeButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            executeButton.Location = new Point(29, 412);
            executeButton.Name = "executeButton";
            executeButton.Size = new Size(160, 29);
            executeButton.TabIndex = 0;
            executeButton.Text = "Execute";
            executeButton.UseVisualStyleBackColor = true;
            executeButton.Click += ExecuteButton_Click;
            // 
            // Chip8Gui
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(executeButton);
            Name = "Chip8Gui";
            Text = "Form1";
            KeyDown += HandleKeyDown;
            KeyUp += HandleKeyUp;
            KeyPreview = true;
            ResumeLayout(false);
        }

        #endregion

        private Button executeButton;
    }
}
