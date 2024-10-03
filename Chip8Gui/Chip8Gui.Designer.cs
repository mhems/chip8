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
            offButton = new Button();
            onButton = new Button();
            offColorPanel = new Panel();
            onColorPanel = new Panel();
            shiftIgnoresYCheckbox = new CheckBox();
            loadProgramButton = new Button();
            bitwiseResetCheckbox = new CheckBox();
            jumpV0Checkbox = new CheckBox();
            memoryIncrementsCheckbox = new CheckBox();
            loadedRomLabel = new Label();
            SuspendLayout();
            // 
            // executeButton
            // 
            executeButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            executeButton.Location = new Point(29, 715);
            executeButton.Name = "executeButton";
            executeButton.Size = new Size(160, 29);
            executeButton.TabIndex = 0;
            executeButton.Text = "Execute";
            executeButton.UseVisualStyleBackColor = true;
            executeButton.Click += ExecuteButton_Click;
            // 
            // offButton
            // 
            offButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            offButton.Location = new Point(1137, 680);
            offButton.Name = "offButton";
            offButton.Size = new Size(94, 29);
            offButton.TabIndex = 1;
            offButton.Text = "Off Color...";
            offButton.UseVisualStyleBackColor = true;
            offButton.Click += OffButton_Click;
            // 
            // onButton
            // 
            onButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            onButton.Location = new Point(1137, 715);
            onButton.Name = "onButton";
            onButton.Size = new Size(94, 29);
            onButton.TabIndex = 2;
            onButton.Text = "On Color...";
            onButton.UseVisualStyleBackColor = true;
            onButton.Click += OnButton_Click;
            // 
            // offColorPanel
            // 
            offColorPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            offColorPanel.BackColor = Color.Black;
            offColorPanel.Location = new Point(1237, 680);
            offColorPanel.Name = "offColorPanel";
            offColorPanel.Size = new Size(29, 29);
            offColorPanel.TabIndex = 3;
            // 
            // onColorPanel
            // 
            onColorPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            onColorPanel.BackColor = Color.White;
            onColorPanel.Location = new Point(1237, 715);
            onColorPanel.Name = "onColorPanel";
            onColorPanel.Size = new Size(29, 29);
            onColorPanel.TabIndex = 4;
            // 
            // shiftIgnoresYCheckbox
            // 
            shiftIgnoresYCheckbox.Anchor = AnchorStyles.Bottom;
            shiftIgnoresYCheckbox.AutoSize = true;
            shiftIgnoresYCheckbox.Location = new Point(488, 685);
            shiftIgnoresYCheckbox.Name = "shiftIgnoresYCheckbox";
            shiftIgnoresYCheckbox.Size = new Size(126, 24);
            shiftIgnoresYCheckbox.TabIndex = 5;
            shiftIgnoresYCheckbox.Text = "Shifts Ignore Y";
            shiftIgnoresYCheckbox.UseVisualStyleBackColor = true;
            shiftIgnoresYCheckbox.CheckedChanged += ShiftIgnoresYCheckbox_CheckedChanged;
            // 
            // loadProgramButton
            // 
            loadProgramButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            loadProgramButton.Location = new Point(29, 680);
            loadProgramButton.Name = "loadProgramButton";
            loadProgramButton.Size = new Size(160, 29);
            loadProgramButton.TabIndex = 6;
            loadProgramButton.Text = "Load Program...";
            loadProgramButton.UseVisualStyleBackColor = true;
            loadProgramButton.Click += LoadProgramButton_Click;
            // 
            // bitwiseResetCheckbox
            // 
            bitwiseResetCheckbox.Anchor = AnchorStyles.Bottom;
            bitwiseResetCheckbox.AutoSize = true;
            bitwiseResetCheckbox.Checked = true;
            bitwiseResetCheckbox.CheckState = CheckState.Checked;
            bitwiseResetCheckbox.Location = new Point(632, 685);
            bitwiseResetCheckbox.Name = "bitwiseResetCheckbox";
            bitwiseResetCheckbox.Size = new Size(162, 24);
            bitwiseResetCheckbox.TabIndex = 7;
            bitwiseResetCheckbox.Text = "Bitwise Ops reset vF";
            bitwiseResetCheckbox.UseVisualStyleBackColor = true;
            bitwiseResetCheckbox.CheckedChanged += BitwiseResetCheckbox_CheckedChanged;
            // 
            // jumpV0Checkbox
            // 
            jumpV0Checkbox.Anchor = AnchorStyles.Bottom;
            jumpV0Checkbox.AutoSize = true;
            jumpV0Checkbox.Checked = true;
            jumpV0Checkbox.CheckState = CheckState.Checked;
            jumpV0Checkbox.Location = new Point(488, 718);
            jumpV0Checkbox.Name = "jumpV0Checkbox";
            jumpV0Checkbox.Size = new Size(117, 24);
            jumpV0Checkbox.TabIndex = 8;
            jumpV0Checkbox.Text = "Jump uses v0";
            jumpV0Checkbox.UseVisualStyleBackColor = true;
            jumpV0Checkbox.CheckedChanged += JumpV0Checkbox_CheckedChanged;
            // 
            // memoryIncrementsCheckbox
            // 
            memoryIncrementsCheckbox.Anchor = AnchorStyles.Bottom;
            memoryIncrementsCheckbox.AutoSize = true;
            memoryIncrementsCheckbox.Checked = true;
            memoryIncrementsCheckbox.CheckState = CheckState.Checked;
            memoryIncrementsCheckbox.Location = new Point(632, 718);
            memoryIncrementsCheckbox.Name = "memoryIncrementsCheckbox";
            memoryIncrementsCheckbox.Size = new Size(170, 24);
            memoryIncrementsCheckbox.TabIndex = 9;
            memoryIncrementsCheckbox.Text = "Memory Increments I";
            memoryIncrementsCheckbox.UseVisualStyleBackColor = true;
            memoryIncrementsCheckbox.CheckedChanged += MemoryIncrementsCheckbox_CheckedChanged;
            // 
            // loadedRomLabel
            // 
            loadedRomLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            loadedRomLabel.AutoSize = true;
            loadedRomLabel.Location = new Point(195, 684);
            loadedRomLabel.Name = "loadedRomLabel";
            loadedRomLabel.Size = new Size(0, 20);
            loadedRomLabel.TabIndex = 10;
            // 
            // Chip8Gui
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1282, 753);
            Controls.Add(loadedRomLabel);
            Controls.Add(memoryIncrementsCheckbox);
            Controls.Add(jumpV0Checkbox);
            Controls.Add(bitwiseResetCheckbox);
            Controls.Add(loadProgramButton);
            Controls.Add(shiftIgnoresYCheckbox);
            Controls.Add(onColorPanel);
            Controls.Add(offColorPanel);
            Controls.Add(onButton);
            Controls.Add(offButton);
            Controls.Add(executeButton);
            KeyPreview = true;
            Name = "Chip8Gui";
            Text = "Chip8 Emulator";
            KeyDown += HandleKeyDown;
            KeyUp += HandleKeyUp;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button executeButton;
        private Button offButton;
        private Button onButton;
        private Panel offColorPanel;
        private Panel onColorPanel;
        private CheckBox shiftIgnoresYCheckbox;
        private Button loadProgramButton;
        private CheckBox bitwiseResetCheckbox;
        private CheckBox jumpV0Checkbox;
        private CheckBox memoryIncrementsCheckbox;
        private Label loadedRomLabel;
    }
}
