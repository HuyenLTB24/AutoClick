using System.Text.Json;
using AutoClick.Models;
using Microsoft.Extensions.Logging;

#if WINDOWS
using System.Drawing;
using System.Windows.Forms;

namespace AutoClick.UI;

public partial class CoordinatePickerForm : Form
{
    private readonly ILogger _logger;
    private readonly string _deviceSerial;
    private readonly string _screenshotPath;
    private readonly Config _config;
    private readonly List<CoordinateSelection> _selectedCoordinates = new();
    
    private PictureBox _pictureBox;
    private Button _saveButton;
    private Button _cancelButton;
    private ListBox _coordinatesList;
    private TextBox _labelTextBox;
    private Label _instructionLabel;
    
    public List<CoordinateSelection> SelectedCoordinates => _selectedCoordinates;
    public bool SaveRequested { get; private set; }

    public CoordinatePickerForm(ILogger logger, string deviceSerial, string screenshotPath, Config config)
    {
        _logger = logger;
        _deviceSerial = deviceSerial;
        _screenshotPath = screenshotPath;
        _config = config;
        
        InitializeComponent();
        LoadScreenshot();
    }

    private void InitializeComponent()
    {
        Text = $"Coordinate Picker - {_deviceSerial}";
        Size = _config.PickCoordsWindowSize != null && _config.PickCoordsWindowSize.Length >= 2
            ? new Size(_config.PickCoordsWindowSize[0], _config.PickCoordsWindowSize[1])
            : new Size(800, 600);
        
        StartPosition = FormStartPosition.CenterScreen;

        // Main layout
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));

        // Left panel for screenshot
        var leftPanel = new Panel { Dock = DockStyle.Fill };
        _pictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle
        };
        
        _pictureBox.MouseClick += PictureBox_MouseClick;
        leftPanel.Controls.Add(_pictureBox);

        // Right panel for controls
        var rightPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 6,
            ColumnCount = 1,
            Padding = new Padding(10)
        };

        _instructionLabel = new Label
        {
            Text = "1. Enter label below\n2. Click on screenshot\n3. Repeat for all coordinates\n4. Click Save",
            Dock = DockStyle.Fill,
            AutoSize = false
        };

        var labelPanel = new Panel { Dock = DockStyle.Fill, Height = 60 };
        var labelLabel = new Label { Text = "Coordinate Label:", Dock = DockStyle.Top };
        _labelTextBox = new TextBox { Dock = DockStyle.Bottom, PlaceholderText = "e.g., start, skip, confirm" };
        labelPanel.Controls.AddRange(new Control[] { _labelTextBox, labelLabel });

        _coordinatesList = new ListBox
        {
            Dock = DockStyle.Fill,
            SelectionMode = SelectionMode.One
        };

        var deleteButton = new Button
        {
            Text = "Delete Selected",
            Dock = DockStyle.Fill,
            Height = 30
        };
        deleteButton.Click += DeleteButton_Click;

        var buttonPanel = new Panel { Dock = DockStyle.Fill, Height = 40 };
        _saveButton = new Button
        {
            Text = "Save",
            Dock = DockStyle.Left,
            Width = 80,
            DialogResult = DialogResult.OK
        };
        _saveButton.Click += SaveButton_Click;

        _cancelButton = new Button
        {
            Text = "Cancel",
            Dock = DockStyle.Right,
            Width = 80,
            DialogResult = DialogResult.Cancel
        };

        buttonPanel.Controls.AddRange(new Control[] { _saveButton, _cancelButton });

        rightPanel.Controls.AddRange(new Control[] 
        { 
            _instructionLabel, labelPanel, _coordinatesList, deleteButton, buttonPanel 
        });

        mainPanel.Controls.Add(leftPanel, 0, 0);
        mainPanel.Controls.Add(rightPanel, 1, 0);
        
        Controls.Add(mainPanel);
    }

    private void LoadScreenshot()
    {
        try
        {
            if (File.Exists(_screenshotPath))
            {
                _pictureBox.Image = Image.FromFile(_screenshotPath);
            }
            else
            {
                MessageBox.Show($"Screenshot not found: {_screenshotPath}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading screenshot: {Path}", _screenshotPath);
            MessageBox.Show($"Error loading screenshot: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void PictureBox_MouseClick(object sender, MouseEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_labelTextBox.Text))
        {
            MessageBox.Show("Please enter a label for this coordinate.", "Label Required", 
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _labelTextBox.Focus();
            return;
        }

        // Convert click coordinates to image coordinates
        var pictureBox = (PictureBox)sender;
        var image = pictureBox.Image;
        if (image == null) return;

        // Calculate the actual image coordinates considering zoom
        var imageX = (int)(e.X * (double)image.Width / pictureBox.ClientSize.Width);
        var imageY = (int)(e.Y * (double)image.Height / pictureBox.ClientSize.Height);

        var selection = new CoordinateSelection
        {
            Label = _labelTextBox.Text.Trim(),
            X = imageX,
            Y = imageY
        };

        _selectedCoordinates.Add(selection);
        UpdateCoordinatesList();

        _logger.LogInformation("Coordinate selected: {Label} at ({X},{Y})", selection.Label, selection.X, selection.Y);
        
        // Clear the label textbox for next coordinate
        _labelTextBox.Clear();
        _labelTextBox.Focus();
    }

    private void UpdateCoordinatesList()
    {
        _coordinatesList.Items.Clear();
        foreach (var coord in _selectedCoordinates)
        {
            _coordinatesList.Items.Add($"{coord.Label}: ({coord.X}, {coord.Y})");
        }
    }

    private void DeleteButton_Click(object sender, EventArgs e)
    {
        if (_coordinatesList.SelectedIndex >= 0 && _coordinatesList.SelectedIndex < _selectedCoordinates.Count)
        {
            var index = _coordinatesList.SelectedIndex;
            _selectedCoordinates.RemoveAt(index);
            UpdateCoordinatesList();
        }
    }

    private void SaveButton_Click(object sender, EventArgs e)
    {
        SaveRequested = true;
        Close();
    }
}
#endif