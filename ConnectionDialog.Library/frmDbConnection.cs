using ConnectionDialog.Library.Extensions;
using ConnectionDialog.Library.Models;
using ConnectionDialog.Library.Services;
using JsonSettings;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinForms.Library.Models;
using static ConnectionDialog.Library.Models.ConnectionDialogSettings;

namespace ConnectionDialog.Library
{
    public partial class frmDbConnection : Form
    {
        private ConnectionDialogSettings _settings = null;
        private ConnectionStringManager _connectionStringManager = null;

        public frmDbConnection()
        {
            InitializeComponent();
        }

        public string SettingsCompanyName { get; set; }
        public string SettingsProductName { get; set; }
        public string DatabaseVendor { get; set; }
        public bool AllowWindowsAuthentication { get { return rbAuthenticationWindows.Enabled; } set { rbAuthenticationWindows.Enabled = value; } }
        public Func<string, IDbConnection> ConnectionMethod { get; set; }
        public Func<IDbConnection, Task<IEnumerable<string>>> ListDatabasesMethod { get; set; }
        public Func<IDbConnection, string, Task> CreateDatabaseMethod { get; set; }

        private ConnectionStringManager ConnectionStringManager
        {
            get
            {
                if (_connectionStringManager == null) _connectionStringManager = new ConnectionStringManager(SettingsCompanyName, SettingsProductName);
                return _connectionStringManager;
            }
        }

        public string ConnectionString
        {
            set
            {
                if (string.IsNullOrEmpty(value)) return;

                string rawConnectionString = ConnectionStringManager.Decrypt(value);
                var parts = ParseConnectionString(rawConnectionString);
                TrySet(parts, "Data Source", (item) => cbServer.Text = item);
                TrySet(parts, "Database", (item) => cbDatabase.Text = item);
                TrySet(parts, "User Id", (item) => cbUser.Text = item);
                TrySet(parts, "Password", (item) => tbPassword.Text = item);
                rbAuthenticationWindows.Checked = (parts.ContainsKey("Integrated Security") && parts["Integrated Security"].Equals("true"));
            }
            get
            {
                return ConnectionStringManager.EncryptIfPasswordPresent(GetConnectionString());
            }
        }

        private string GetConnectionString()
        {
            string auth = (rbAuthenticationWindows.Checked) ? "Integrated Security=true" : $"User Id={cbUser.Text};Password={tbPassword.Text}";
            return $"Data Source={cbServer.Text};Database={cbDatabase.Text};{auth}";
        }

        private void TrySet(Dictionary<string, string> parts, string key, Action<string> setControl)
        {
            try
            {
                string value = parts[key];
                setControl.Invoke(value);
            }
            catch
            {
                // do nothing
            }
        }

        private Dictionary<string, string> ParseConnectionString(string value)
        {
            return value.Split(';').Where(s => HasTwoParts(s)).Select(s =>
            {
                string[] parts = s.Split('=');
                return new KeyValuePair<string, string>(parts[0], parts[1]);
            }).ToDictionary(row => row.Key, row => row.Value);
        }

        private bool HasTwoParts(string s)
        {
            try
            {
                string[] parts = s.Split('=');
                return (parts.Length == 2);
            }
            catch
            {
                return false;
            }
        }

        private void frmDbConnection_Load(object sender, EventArgs e)
        {
            try
            {
                _settings = JsonSettingsBase.Load<ConnectionDialogSettings>();
                var serverNames = _settings?.Servers?.Select(s => new ListItem<Server>(s, s.Name)).ToArray();
                if (serverNames != null) cbServer.Items.AddRange(serverNames);

                if (!AllowWindowsAuthentication)
                {
                    rbAuthenticationDb.Checked = true;
                    rbAuthenticationWindows.Enabled = false;
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.FullMessage());
            }
        }

        private void frmDbConnection_FormClosing(object sender, FormClosingEventArgs e)
        {
            _settings.Save();
        }

        private void cbServer_SelectedIndexChanged(object sender, EventArgs e)
        {
            cbUser.Items.Clear();
            if (cbServer.SelectedItem != null)
            {
                cbUser.Items.Clear();
                var serverItem = cbServer.SelectedItem as ListItem<Server>;
                if (serverItem != null)
                {
                    var users = serverItem.Value.Users.Where(u => !string.IsNullOrEmpty(u.Name)).Select(u => new ListItem<User>(u, u.Name)).ToArray();
                    cbUser.Items.AddRange(users);
                }
            }
        }

        private void btnTest_Click(object sender, EventArgs e)
        {
            if (TestConnection(out string message))
            {
                llCreateDb.Visible = false;
                MessageBox.Show("Connection opened successfully.");
            }
            else
            {
                llCreateDb.Visible = true;
                MessageBox.Show(message);
            }
        }

        private bool TestConnectionWithoutDatabase()
        {
            try
            {
                string connectionString = GetConnectionWithoutDatabase();
                using (var cn = ConnectionMethod.Invoke(connectionString))
                {
                    cn.Open();
                    cn.Close();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private string GetConnectionWithoutDatabase()
        {
            string auth = (rbAuthenticationWindows.Checked) ? "Integrated Security=true" : $"User Id={cbUser.Text};Password={tbPassword.Text}";
            return $"Data Source={cbServer.Text};{auth}";
        }

        private bool TestConnection(out string message, bool saveSettings = true, int retries = 1)
        {
            message = null;
            for (int attempt = 0; attempt < retries; attempt++)
            {
                try
                {
                    using (var cn = ConnectionMethod.Invoke(GetConnectionString()))
                    {
                        cn.Open();
                        cn.Close();
                        if (saveSettings)
                        {
                            var server = _settings.AddServer(cbServer.Text);
                            if (rbAuthenticationDb.Checked) server.AddUser(cbUser.Text, tbPassword.Text);
                        }
                        return true;
                    }
                }
                catch (Exception exc)
                {
                    message = exc.Message;
                    if (retries > 1) Thread.Sleep(250);
                }
            }

            return false;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (TestConnection(out string message))
            {
                DialogResult = DialogResult.OK;
            }
            else
            {
                MessageBox.Show("Connection failed: " + message);
            }
        }

        private async void rbAuthenticationWindows_CheckedChanged(object sender, EventArgs e)
        {
            cbUser.Enabled = !rbAuthenticationWindows.Checked;
            tbPassword.Enabled = !rbAuthenticationWindows.Checked;
            if (rbAuthenticationWindows.Checked) await FillDatabaseListAsync();
        }

        private void rbAuthenticationDb_CheckedChanged(object sender, EventArgs e)
        {
            cbUser.Enabled = rbAuthenticationDb.Checked;
            tbPassword.Enabled = rbAuthenticationDb.Checked;
            if (rbAuthenticationDb.Checked) cbUser.Focus();
        }

        private async void cbUser_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                var userItem = cbUser.SelectedItem as ListItem<User>;
                if (userItem != null)
                {
                    tbPassword.Text = userItem.Value.Password;
                    await FillDatabaseListAsync();
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.FullMessage());
            }
        }

        private async Task FillDatabaseListAsync()
        {
            cbDatabase.Items.Clear();
            if (TestConnectionWithoutDatabase())
            {
                llCreateDb.Visible = false;
                using (var cn = ConnectionMethod.Invoke(GetConnectionWithoutDatabase()))
                {
                    var databases = await ListDatabasesMethod.Invoke(cn);
                    cbDatabase.Items.Clear();
                    cbDatabase.Items.AddRange(databases?.ToArray());
                }
            }
            else
            {
                llCreateDb.Visible = true;
            }
        }

        private async void llCreateDb_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                using (var cn = ConnectionMethod.Invoke(GetConnectionWithoutDatabase()))
                {
                    await CreateDatabaseMethod.Invoke(cn, cbDatabase.Text);
                    if (TestConnection(out string message, retries: 30))
                    {
                        MessageBox.Show("Database created successfully.");
                    }
                    else
                    {
                        MessageBox.Show("Database create field: " + message);
                    }
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.FullMessage());
            }
        }
    }
}