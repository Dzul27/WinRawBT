using WinRawBT.Helpers;

namespace WinRawBT.Forms;

/// <summary>
/// Chooses which websites may print: everyone (default) or only a list of origins.
/// Applied live by <see cref="MainForm"/>; no service restart needed.
/// </summary>
public sealed class OriginsForm : Form
{
    private readonly RadioButton _rbAll = new();
    private readonly RadioButton _rbList = new();
    private readonly ListBox _list = new();
    private readonly TextBox _txtNew = new();
    private readonly Button _btnAdd = new();
    private readonly Button _btnRemove = new();
    private readonly Button _btnOk = new();
    private readonly Button _btnCancel = new();

    public OriginsForm(bool allowAny, IEnumerable<string> origins)
    {
        Text = "Allowed Websites";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new System.Drawing.Size(456, 372);

        _rbAll.Text = "Allow all websites (easiest, default)";
        _rbAll.AutoSize = true;
        _rbAll.Location = new System.Drawing.Point(14, 14);
        _rbAll.CheckedChanged += (_, _) => UpdateEnabled();

        var lblAll = new Label
        {
            Text = "Any web POS works with no setup. Any website open on this PC can also print.",
            Location = new System.Drawing.Point(34, 38),
            Size = new System.Drawing.Size(410, 32),
        };

        _rbList.Text = "Only allow these websites:";
        _rbList.AutoSize = true;
        _rbList.Location = new System.Drawing.Point(14, 82);

        _list.Location = new System.Drawing.Point(34, 108);
        _list.Size = new System.Drawing.Size(410, 140);
        _list.HorizontalScrollbar = true;
        _list.SelectionMode = SelectionMode.One;
        foreach (string origin in origins)
        {
            string? normalized = OriginHelper.Normalize(origin);
            if (normalized != null && !_list.Items.Contains(normalized))
            {
                _list.Items.Add(normalized);
            }
        }

        _txtNew.Location = new System.Drawing.Point(34, 258);
        _txtNew.Size = new System.Drawing.Size(250, 23);
        _txtNew.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                TryAddPending();
            }
        };

        _btnAdd.Text = "Add";
        _btnAdd.Location = new System.Drawing.Point(290, 257);
        _btnAdd.Size = new System.Drawing.Size(70, 25);
        _btnAdd.UseVisualStyleBackColor = true;
        _btnAdd.Click += (_, _) => TryAddPending();

        _btnRemove.Text = "Remove";
        _btnRemove.Location = new System.Drawing.Point(366, 257);
        _btnRemove.Size = new System.Drawing.Size(78, 25);
        _btnRemove.UseVisualStyleBackColor = true;
        _btnRemove.Click += (_, _) =>
        {
            int index = _list.SelectedIndex;
            if (index >= 0)
            {
                _list.Items.RemoveAt(index);
                if (_list.Items.Count > 0)
                {
                    _list.SelectedIndex = Math.Min(index, _list.Items.Count - 1);
                }
            }
        };

        var lblHint = new Label
        {
            Text = "Type a site address, e.g. pos.example.com or https://pos.example.com. " +
                   "Use \"null\" for pages opened as a local file. Changes apply immediately.",
            Location = new System.Drawing.Point(34, 290),
            Size = new System.Drawing.Size(410, 48),
        };

        _btnOk.Text = "OK";
        _btnOk.Location = new System.Drawing.Point(286, 338);
        _btnOk.Size = new System.Drawing.Size(76, 26);
        _btnOk.UseVisualStyleBackColor = true;
        _btnOk.Click += (_, _) => Confirm();

        _btnCancel.Text = "Cancel";
        _btnCancel.Location = new System.Drawing.Point(368, 338);
        _btnCancel.Size = new System.Drawing.Size(76, 26);
        _btnCancel.UseVisualStyleBackColor = true;
        _btnCancel.DialogResult = DialogResult.Cancel;

        CancelButton = _btnCancel;
        Controls.AddRange(new Control[]
        {
            _rbAll, lblAll, _rbList, _list, _txtNew, _btnAdd, _btnRemove, lblHint, _btnOk, _btnCancel,
        });

        _rbAll.Checked = allowAny;
        _rbList.Checked = !allowAny;
        UpdateEnabled();
    }

    /// <summary>True when every website is allowed.</summary>
    public bool AllowAny => _rbAll.Checked;

    /// <summary>Normalized origins in the list (kept even when "allow all" is chosen).</summary>
    public List<string> Origins => _list.Items.Cast<string>().ToList();

    private void UpdateEnabled()
    {
        bool listMode = _rbList.Checked;
        _list.Enabled = listMode;
        _txtNew.Enabled = listMode;
        _btnAdd.Enabled = listMode;
        _btnRemove.Enabled = listMode;
    }

    /// <summary>Adds the typed address to the list. Returns false (with a message) if it is not valid.</summary>
    private bool TryAddPending()
    {
        string typed = _txtNew.Text;
        if (string.IsNullOrWhiteSpace(typed))
        {
            return true;
        }

        string? origin = OriginHelper.Normalize(typed);
        if (origin == null)
        {
            MessageBox.Show(this,
                $"\"{typed.Trim()}\" is not a valid website address.\n\nExample: pos.example.com or https://pos.example.com",
                "WinRawBT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtNew.Focus();
            return false;
        }

        if (!_list.Items.Contains(origin))
        {
            _list.Items.Add(origin);
        }

        _list.SelectedItem = origin;
        _txtNew.Clear();
        _txtNew.Focus();
        return true;
    }

    private void Confirm()
    {
        if (_rbList.Checked)
        {
            if (!TryAddPending())
            {
                return;
            }

            if (_list.Items.Count == 0)
            {
                MessageBox.Show(this,
                    "The list is empty, so every web POS would be blocked. Add a website or choose \"Allow all websites\".",
                    "WinRawBT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        DialogResult = DialogResult.OK;
    }
}
