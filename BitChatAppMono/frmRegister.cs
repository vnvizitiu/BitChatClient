﻿/*
Technitium Bit Chat
Copyright (C) 2015  Shreyas Zare (shreyas@technitium.com)

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

*/

using BitChatCore;
using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Windows.Forms;
using TechnitiumLibrary.Net.Proxy;
using TechnitiumLibrary.Security.Cryptography;

namespace BitChatApp
{
    public partial class frmRegister : Form
    {
        #region variables

        BitChatProfile _profile;
        string _profileFilePath;
        bool _isPortableApp;
        string _profileFolder;

        RSAParameters _parameters;

        bool _enableProxy;
        NetProxyType _proxyType;
        string _proxyAddress;
        int _proxyPort;
        NetworkCredential _proxyCredentials;

        #endregion

        #region constructor

        public frmRegister(bool isPortableApp, string profileFolder)
        {
            _isPortableApp = isPortableApp;
            _profileFolder = profileFolder;

            InitializeComponent();

            this.chkEnableProxy.CheckedChanged += new System.EventHandler(this.chkEnableProxy_CheckedChanged);
        }

        public frmRegister(BitChatProfile profile, string profileFilePath, bool isPortableApp, string profileFolder, bool reissue)
        {
            _profile = profile;
            _profileFilePath = profileFilePath;
            _isPortableApp = isPortableApp;
            _profileFolder = profileFolder;

            if (profile.Proxy != null)
                _proxyType = profile.Proxy.Type;

            _enableProxy = (_profile.Proxy != null);
            _proxyAddress = _profile.ProxyAddress;
            _proxyPort = _profile.ProxyPort;
            _proxyCredentials = _profile.ProxyCredentials;

            InitializeComponent();

            chkEnableProxy.Checked = _enableProxy;
            this.chkEnableProxy.CheckedChanged += new System.EventHandler(this.chkEnableProxy_CheckedChanged);

            if (reissue)
            {
                CertificateProfile certProfile = _profile.LocalCertificateStore.Certificate.IssuedTo;

                txtName.Text = certProfile.Name;
                txtEmail.Text = certProfile.EmailAddress.Address;
                txtEmail.ReadOnly = true;

                if (certProfile.Website != null)
                    txtWebsite.Text = certProfile.Website.AbsoluteUri;

                txtPhone.Text = certProfile.PhoneNumber;
                txtStreetAddress.Text = certProfile.StreetAddress;
                txtCity.Text = certProfile.City;
                txtState.Text = certProfile.State;
                txtCountry.Text = certProfile.Country;
                txtPostalCode.Text = certProfile.PostalCode;
            }
            else
            {
                lblRegisteredEmail.Text = _profile.LocalCertificateStore.Certificate.IssuedTo.EmailAddress.Address;

                pnlRegister.Visible = false;
                pnlDownloadCert.Visible = true;
            }
        }

        #endregion

        #region private

        private void frmRegister_Load(object sender, EventArgs e)
        {
            if ((Environment.OSVersion.Platform == PlatformID.Win32NT) && (Environment.OSVersion.Version.Major < 6))
            {
                MessageBox.Show("Registration of profile certificate may fail on Windows XP with an SSL/TLS error. Registration process connects to a web server which uses SSL/TLS for secure HTTPS connections and Windows XP with older Service Pack may not support latest version of TLS, and also may not trust the root certificate installed on the web server. Due to this fact, registration process may fail with an certificate error. However, you can import an already registered profile certificate and use it on Windows XP.", "Windows XP SSL/TLS Issue", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void rbImportRSA_CheckedChanged(object sender, EventArgs e)
        {
            if (rbImportRSA.Checked)
            {
                using (frmImportPEM frm = new frmImportPEM())
                {
                    if (frm.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                    {
                        _parameters = frm.Parameters;
                    }
                    else
                    {
                        rbAutoGenRSA.Checked = true;
                        rbImportRSA.Checked = false;
                    }
                }
            }
        }

        private void chkEnableProxy_CheckedChanged(object sender, EventArgs e)
        {
            if (chkEnableProxy.Checked)
            {
                using (frmProxyConfig frm = new frmProxyConfig(_proxyType, _proxyAddress, _proxyPort, _proxyCredentials))
                {
                    if (frm.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                    {
                        if (frm.ProxyType != NetProxyType.None)
                            _proxyType = frm.ProxyType;

                        _proxyAddress = frm.ProxyAddress;
                        _proxyPort = frm.ProxyPort;
                        _proxyCredentials = frm.ProxyCredentials;

                        chkEnableProxy.Checked = (frm.ProxyType != NetProxyType.None);
                    }
                    else
                    {
                        chkEnableProxy.Checked = false;
                    }
                }
            }

            _enableProxy = chkEnableProxy.Checked;
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure to close this window?", "Close Window?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes)
            {
                this.DialogResult = System.Windows.Forms.DialogResult.Ignore;
                this.Close();
            }
        }

        private void chkAccept_CheckedChanged(object sender, EventArgs e)
        {
            btnRegister.Enabled = chkAccept.Checked;
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(@"http://go.technitium.com/?id=3");
        }

        private void btnRegister_Click(object sender, EventArgs e)
        {
            string name = null;
            MailAddress emailAddress = null;
            Uri website = null;
            string phoneNumber = null;
            string streetAddress = null;
            string city = null;
            string state = null;
            string country = null;
            string postalCode = null;

            #region validate form

            if (string.IsNullOrEmpty(txtName.Text))
            {
                MessageBox.Show("Please enter a valid name.", "Name Missing!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                txtName.Focus();
                return;
            }

            if (string.IsNullOrEmpty(txtEmail.Text))
            {
                MessageBox.Show("Please enter a valid email address.", "Email Address Missing!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                txtEmail.Focus();
                return;
            }
            else
            {
                try
                {
                    emailAddress = new MailAddress(txtEmail.Text);
                }
                catch
                {
                    MessageBox.Show("Please enter a valid email address.", "Invalid Email Address!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    txtEmail.Focus();
                    return;
                }
            }

            if (!string.IsNullOrEmpty(txtWebsite.Text))
            {
                try
                {
                    website = new Uri(txtWebsite.Text);
                }
                catch
                {
                    MessageBox.Show("Please enter a valid web address. Example: http://example.com", "Invalid Web Address!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    txtWebsite.Focus();
                    return;
                }
            }

            if (string.IsNullOrEmpty(txtCountry.Text))
            {
                MessageBox.Show("Please select a valid country name.", "Country Name Missing!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                txtCountry.Focus();
                return;
            }

            if (string.IsNullOrEmpty(txtProfilePassword.Text))
            {
                MessageBox.Show("Please enter a profile password.", "Profile Password Missing!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                txtProfilePassword.Focus();
                return;
            }

            if (string.IsNullOrEmpty(txtConfirmPassword.Text))
            {
                MessageBox.Show("Please confirm profile password.", "Confirm Password Missing!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                txtConfirmPassword.Focus();
                return;
            }

            if (txtConfirmPassword.Text != txtProfilePassword.Text)
            {
                txtProfilePassword.Text = "";
                txtConfirmPassword.Text = "";
                MessageBox.Show("Profile password doesn't match with confirm profile password. Please enter both passwords again.", "Password Mismatch!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                txtProfilePassword.Focus();
                return;
            }

            #endregion

            if (!string.IsNullOrEmpty(txtName.Text))
                name = txtName.Text;

            if (!string.IsNullOrEmpty(txtPhone.Text))
                phoneNumber = txtPhone.Text;

            if (!string.IsNullOrEmpty(txtStreetAddress.Text))
                streetAddress = txtStreetAddress.Text;

            if (!string.IsNullOrEmpty(txtCity.Text))
                city = txtCity.Text;

            if (!string.IsNullOrEmpty(txtState.Text))
                state = txtState.Text;

            if (!string.IsNullOrEmpty(txtCountry.Text))
                country = txtCountry.Text;

            if (!string.IsNullOrEmpty(txtPostalCode.Text))
                postalCode = txtPostalCode.Text;


            CertificateProfile certProfile = new CertificateProfile(name, CertificateProfileType.Individual, emailAddress, website, phoneNumber, streetAddress, city, state, country, postalCode);

            pnlRegister.Visible = false;
            lblPanelTitle.Text = "Registering...";

            if (rbImportRSA.Checked)
                lblPanelMessage.Text = "Please wait while we register your profile certificate.\r\n\r\nRegistering on https://" + Program.SIGNUP_URI.Host + " ...";
            else
                lblPanelMessage.Text = "Please wait while we generate your profile private key and register your profile certificate.\r\n\r\nRegistering on https://" + Program.SIGNUP_URI.Host + " ...";

            pnlMessages.Visible = true;

            Action<CertificateProfile> d = new Action<CertificateProfile>(RegisterAsync);
            d.BeginInvoke(certProfile, null, null);
        }

        private void btnDownloadAndStart_Click(object sender, EventArgs e)
        {
            try
            {
                _profile.DownloadSignedCertificate(Program.SIGNUP_URI);

                using (FileStream fS = new FileStream(_profileFilePath, FileMode.Create, FileAccess.ReadWrite))
                {
                    _profile.WriteTo(fS);
                }

                this.DialogResult = System.Windows.Forms.DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error orrured while downloading profile certificate:\r\n\r\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RegisterAsync(CertificateProfile certProfile)
        {
            try
            {
                //register
                AsymmetricCryptoKey privateKey;

                if (rbImportRSA.Checked)
                    privateKey = AsymmetricCryptoKey.CreateUsing(_parameters);
                else
                    privateKey = new AsymmetricCryptoKey(AsymmetricEncryptionAlgorithm.RSA, 4096);

                Certificate selfSignedCert = new Certificate(CertificateType.RootCA, "", certProfile, CertificateCapability.SignCACertificate, DateTime.UtcNow, DateTime.UtcNow, AsymmetricEncryptionAlgorithm.RSA, privateKey.GetPublicKey());
                selfSignedCert.SelfSign("SHA256", privateKey, null);

                if (_profile == null)
                    _profile = new BitChatProfile((new Random(DateTime.UtcNow.Millisecond)).Next(1024, 65535), Environment.GetFolderPath(Environment.SpecialFolder.Desktop), BitChatProfile.DefaultTrackerURIs, _isPortableApp, _profileFolder);

                if (_enableProxy)
                    _profile.ConfigureProxy(_proxyType, _proxyAddress, _proxyPort, _proxyCredentials);

                _profile.Register(Program.SIGNUP_URI, new CertificateStore(selfSignedCert, privateKey));
                _profile.SetPassword(SymmetricEncryptionAlgorithm.Rijndael, 256, txtProfilePassword.Text);

                _profileFilePath = Path.Combine(_profileFolder, _profile.LocalCertificateStore.Certificate.IssuedTo.Name + ".profile");

                using (FileStream fS = new FileStream(_profileFilePath, FileMode.Create, FileAccess.ReadWrite))
                {
                    _profile.WriteTo(fS);
                }

                this.Invoke(new Action<object>(RegistrationSuccess), new object[] { null });
            }
            catch (Exception ex)
            {
                this.Invoke(new Action<object>(RegistrationFail), new object[] { ex.Message });
            }
        }

        private void RegistrationSuccess(object state)
        {
            lblRegisteredEmail.Text = _profile.LocalCertificateStore.Certificate.IssuedTo.EmailAddress.Address;

            pnlMessages.Visible = false;
            pnlRegister.Visible = false;
            pnlDownloadCert.Visible = true;
        }

        private void RegistrationFail(object state)
        {
            MessageBox.Show("Error occurred while registering for profile certificate:\r\n\r\n" + (string)state, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            pnlMessages.Visible = false;
            pnlRegister.Visible = true;
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://blog.technitium.com/2015/11/how-to-register-profile-get-started.html");
        }

        #endregion

        #region properties

        public BitChatProfile Profile
        { get { return _profile; } }

        public string ProfileFilePath
        { get { return _profileFilePath; } }

        #endregion
    }
}
