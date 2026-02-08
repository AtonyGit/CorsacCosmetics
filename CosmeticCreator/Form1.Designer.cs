namespace CosmeticCreator;

partial class Form1
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
        components = new System.ComponentModel.Container();
        
        var tableLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            ColumnCount = 2,
            Padding = new Padding(10)
        };
        
        tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        
        // Hat Name
        var hatNameLabel = new Label { Text = "Hat Name:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
        hatNameTextBox = new TextBox { Text = "Test Hat", Dock = DockStyle.Fill };
        tableLayout.Controls.Add(hatNameLabel, 0, 0);
        tableLayout.Controls.Add(hatNameTextBox, 1, 0);
        
        // Main Sprite Path
        var mainSpriteLabel = new Label { Text = "Main Sprite:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
        mainSpriteTextBox = new TextBox { Dock = DockStyle.Fill };
        tableLayout.Controls.Add(mainSpriteLabel, 0, 1);
        tableLayout.Controls.Add(mainSpriteTextBox, 1, 1);
        
        // Preview Sprite Path
        var previewSpriteLabel = new Label { Text = "Preview Sprite:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
        previewSpriteTextBox = new TextBox { Dock = DockStyle.Fill };
        tableLayout.Controls.Add(previewSpriteLabel, 0, 2);
        tableLayout.Controls.Add(previewSpriteTextBox, 1, 2);
        
        // Output Path
        var outputPathLabel = new Label { Text = "Output Path:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
        var outputPathPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        outputPathTextBox = new TextBox { Width = 400, Text = "test_bundle.ccb" };
        var browseButton = new Button { Text = "Browse", Width = 80, Height = 23 };
        browseButton.Click += (s, e) => BrowseOutputPath();
        outputPathPanel.Controls.Add(outputPathTextBox);
        outputPathPanel.Controls.Add(browseButton);
        tableLayout.Controls.Add(outputPathLabel, 0, 3);
        tableLayout.Controls.Add(outputPathPanel, 1, 3);
        
        // Button Panel
        var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        var createButton = new Button { Text = "Create Bundle", Width = 100, Height = 30 };
        createButton.Click += (s, e) => CreateBundle();
        var statusLabel = new Label { AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
        statusTextBlock = statusLabel;
        buttonPanel.Controls.Add(createButton);
        buttonPanel.Controls.Add(statusLabel);
        tableLayout.Controls.Add(buttonPanel, 0, 4);
        tableLayout.SetColumnSpan(buttonPanel, 2);
        
        Controls.Add(tableLayout);
        
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(700, 300);
        Text = "Bundle Creator";
        StartPosition = FormStartPosition.CenterScreen;
    }
    
    private TextBox hatNameTextBox;
    private TextBox mainSpriteTextBox;
    private TextBox previewSpriteTextBox;
    private TextBox outputPathTextBox;
    private Label statusTextBlock;

    #endregion
}