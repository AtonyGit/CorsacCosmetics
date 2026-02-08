using System.Text.Json;

namespace CosmeticCreator;

public partial class Form1 : Form
{
    public Form1()
    {
        InitializeComponent();
    }

    private void BrowseOutputPath()
    {
        using var saveDialog = new SaveFileDialog
        {
            Filter = "Corsac Bundle Files (*.ccb)|*.ccb|All Files (*.*)|*.*",
            DefaultExt = "ccb",
            FileName = outputPathTextBox.Text
        };

        if (saveDialog.ShowDialog() == DialogResult.OK)
        {
            outputPathTextBox.Text = saveDialog.FileName;
        }
    }

    private void CreateBundle()
    {
        try
        {
            var hatName = hatNameTextBox.Text;
            var mainSpritePath = mainSpriteTextBox.Text;
            var previewSpritePath = previewSpriteTextBox.Text;
            var outputPath = outputPathTextBox.Text;

            // Validate inputs
            if (string.IsNullOrWhiteSpace(hatName))
            {
                statusTextBlock.Text = "Error: Hat name cannot be empty";
                return;
            }

            // Load sprite files
            byte[] mainSpriteBytes = LoadSpriteBytes(mainSpritePath);
            byte[] previewSpriteBytes = LoadSpriteBytes(previewSpritePath);

            if (mainSpriteBytes.Length == 0 || previewSpriteBytes.Length == 0)
            {
                statusTextBlock.Text = "Error: Could not load sprite files";
                return;
            }

            // Create and write bundle file
            WriteBundleFile(outputPath, hatName, mainSpriteBytes, previewSpriteBytes);

            statusTextBlock.Text = $"Success: Bundle created at {outputPath}";
        }
        catch (Exception ex)
        {
            statusTextBlock.Text = $"Error: {ex.Message}";
        }
    }

    private byte[] LoadSpriteBytes(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return [];
        }

        return File.ReadAllBytes(filePath);
    }

    private void WriteBundleFile(string outputPath, string hatName, byte[] mainSpriteBytes, byte[] previewSpriteBytes)
    {
        using var ms = new MemoryStream();

        // Build sprite data entries
        uint dataOffset = 0;
        
        var previewData = new SpriteData { Offset = dataOffset, Size = (uint)previewSpriteBytes.Length };
        dataOffset += (uint)previewSpriteBytes.Length;
        
        var mainData = new SpriteData { Offset = dataOffset, Size = (uint)mainSpriteBytes.Length };

        // Create hat manifest
        var hatManifest = new HatManifest
        {
            Name = hatName,
            MatchPlayerColor = false,
            BlocksVisors = false,
            InFront = true,
            NoBounce = true,
            PreviewSprite = previewData,
            MainSprite = mainData,
            BackSprite = mainData,
            ClimbSprite = mainData,
            FloorSprite = mainData,
            LeftMainSprite = mainData,
            LeftBackSprite = mainData,
            LeftClimbSprite = mainData,
            LeftFloorSprite = mainData
        };

        // Create bundle manifest
        var bundleManifest = new BundleManifest
        {
            Version = 1,
            Hats = [hatManifest]
        };

        // Serialize manifest
        var options = new JsonSerializerOptions { WriteIndented = false };
        var manifestJson = JsonSerializer.Serialize(bundleManifest, options);
        var manifestBytes = System.Text.Encoding.UTF8.GetBytes(manifestJson);

        // Calculate total data length
        uint totalDataLength = (uint)previewSpriteBytes.Length + (uint)mainSpriteBytes.Length;

        // Create header
        var header = new BundleHeader
        {
            Magic = BundleHeader.ExpectedMagic,
            Version = BundleHeader.CurrentVersion,
            Flags = 0,
            ManifestLength = (uint)manifestBytes.Length,
            DataLength = totalDataLength
        };

        // Write to file
        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        
        // Write header
        BundleHeader.Write(fs, header);

        // Write manifest
        fs.Write(manifestBytes, 0, manifestBytes.Length);

        // Write sprite data
        fs.Write(previewSpriteBytes, 0, previewSpriteBytes.Length);
        fs.Write(mainSpriteBytes, 0, mainSpriteBytes.Length);
    }
}