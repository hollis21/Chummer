﻿using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceModel;
using System.Windows.Forms;
using System.Xml;
using Chummer.OmaeService;

namespace Chummer
{
	public partial class frmOmaeUploadData : Form
	{
		private readonly OmaeHelper _objOmaeHelper = new OmaeHelper();

		// Error message constants.
		private readonly string NO_CONNECTION_MESSAGE = "";
		private readonly string NO_CONNECTION_TITLE = "";

		private string _strUserName;
		private int _intDataID = 0;

		#region Control Events
		public frmOmaeUploadData(string strUserName, int intDataID = 0, string strDescription = "", string strName = "")
		{
			InitializeComponent();
			LanguageManager.Instance.Load(GlobalOptions.Instance.Language, this);
			_strUserName = strUserName;
			_intDataID = intDataID;
			txtDescription.Text = strDescription;
			txtName.Text = strName;

			NO_CONNECTION_MESSAGE = LanguageManager.Instance.GetString("Message_Omae_CannotConnection");
			NO_CONNECTION_TITLE = LanguageManager.Instance.GetString("MessageTitle_Omae_CannotConnection");

			MoveControls();
		}

		private void frmOmaeUploadData_Load(object sender, EventArgs e)
		{
			// Populate the CheckedListBox with the list of custom and override files in the user's data directory.
			string strFilePath = Path.Combine(Application.StartupPath, "data");
			foreach (string strFile in Directory.GetFiles(strFilePath, "custom*_*.xml"))
			{
				TreeNode objNode = new TreeNode();
				objNode.Tag = strFile;
				objNode.Text = strFile.Replace(strFilePath + Path.DirectorySeparatorChar, string.Empty);
				treFiles.Nodes.Add(objNode);
			}

			foreach (string strFile in Directory.GetFiles(strFilePath, "override*_*.xml"))
			{
				TreeNode objNode = new TreeNode();
				objNode.Tag = strFile;
				objNode.Text = strFile.Replace(strFilePath + Path.DirectorySeparatorChar, string.Empty);
				treFiles.Nodes.Add(objNode);
			}
		}

		private void cmdUpload_Click(object sender, EventArgs e)
		{
			// Make sure a name has been entered.
			if (txtName.Text == "")
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_OmaeUpload_DataName"), LanguageManager.Instance.GetString("MessageTitle_OmaeUpload_DataName"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			// Make sure there is at least some sort of description.
			if (txtDescription.Text.Trim() == "")
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_OameUpload_DataDescription"), LanguageManager.Instance.GetString("MessageTitle_OmaeUpload_DataDescription"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			// Make sure at least 1 file is selected.
			int intCount = 0;
			foreach (TreeNode objNode in treFiles.Nodes)
			{
				if (objNode.Checked)
					intCount++;
			}

			if (intCount == 0)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_OmaeUpload_DataSelectFiles"), LanguageManager.Instance.GetString("MessageTitle_OmaeUpload_SelectFile"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			
			bool blnSuccess = false;

			string strFilePath = Path.Combine(Application.StartupPath, "data");
			XmlDocument objXmlBooks = new XmlDocument();
			objXmlBooks.Load(Path.Combine(strFilePath, "books.xml"));

			List<string> lstSource = new List<string>();

			// Run through all of the selected items. Create a list of the <source> items seen and make sure they're available in the core files or included in the user's selected item.
			foreach (TreeNode objNode in treFiles.Nodes)
			{
				if (objNode.Checked)
				{
					XmlDocument objXmlDocument = new XmlDocument();
					objXmlDocument.Load(objNode.Tag.ToString());

					XmlNodeList objRootList = objXmlDocument.SelectNodes("/chummer");
					foreach (XmlNode objXmlGroup in objRootList[0])
					{
						foreach (XmlNode objXmlNode in objXmlGroup.ChildNodes)
						{
							if (objXmlNode["source"] != null)
							{
								// Look to see if this sourcebook is already in the list. If not, add it.
								bool blnFound = false;
								foreach (string strSource in lstSource)
								{
									if (strSource == objXmlNode["source"].InnerText)
									{
										blnFound = true;
										break;
									}
								}
								if (!blnFound)
									lstSource.Add(objXmlNode["source"].InnerText);
							}
						}
					}
				}
			}

			// Now that we have the list of used sourcebooks, check the books file for the items.
			for (int i = 0; i <= lstSource.Count - 1; i++)
			{
				string strSource = lstSource[i];
				if (strSource != "")
				{
					XmlNode objNode = objXmlBooks.SelectSingleNode("/chummer/books/book[code = \"" + strSource + "\"]");
					if (objNode != null)
						lstSource[i] = "";
				}
			}

			// Check any custom book files the user selected.
			foreach (TreeNode objNode in treFiles.Nodes)
			{
				if (objNode.Checked && objNode.Tag.ToString().EndsWith("books.xml"))
				{
					XmlDocument objXmlCustom = new XmlDocument();
					objXmlCustom.Load(objNode.Tag.ToString());

					for (int i = 0; i <= lstSource.Count - 1; i++)
					{
						string strSource = lstSource[i];
						if (strSource != "")
						{
							XmlNode objBookNode = objXmlCustom.SelectSingleNode("/chummer/books/book[code = \"" + strSource + "\"]");
							if (objBookNode != null)
								lstSource[i] = "";
						}
					}
				}
			}

			// With all of the books checked, run through the list one more time and display an error if there are still any that were not found.
			string strMessage = "";
			foreach (string strSource in lstSource)
			{
				if (strSource != "")
					strMessage += "\n\t" + strSource;
			}
			if (strMessage != string.Empty)
			{
				MessageBox.Show("The following sourcebooks could not be found in the core data files or any of the data files you have selected:" + strMessage);
				return;
			}

			// Everything is OK, so zip up the selected files.
			List<string> lstFiles = new List<string>();
			string strFilesIncluded = "";
			foreach (TreeNode objNode in treFiles.Nodes)
			{
				if (objNode.Checked)
				{
					lstFiles.Add(objNode.Tag.ToString());
					strFilesIncluded += objNode.Text + ",";
				}
			}
			byte[] bytFile = _objOmaeHelper.CompressMutiple(lstFiles);

			// Make sure the file doesn't exceed 250K in size (256,000 bytes).
			if (bytFile.Length > 256000)
			{
				MessageBox.Show(LanguageManager.Instance.GetString("Message_OmaeUpload_FileTooLarge"), LanguageManager.Instance.GetString("MessageTitle_OmaeUpload_FileTooLarge"), MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			// Upload the file.
			omaeSoapClient objService = _objOmaeHelper.GetOmaeService();
			try
			{
				cmdUpload.Enabled = false;
				txtDescription.Enabled = false;
				txtName.Enabled = false;
				if (objService.UploadDataFile(_strUserName, _intDataID, txtName.Text, txtDescription.Text, strFilesIncluded, bytFile))
				{
					blnSuccess = true;
					MessageBox.Show(LanguageManager.Instance.GetString("Message_OmaeUpload_UploadComplete"), LanguageManager.Instance.GetString("MessageTitle_OmaeUpload_UploadComplete"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
				else
				{
					MessageBox.Show(LanguageManager.Instance.GetString("Message_OmaeUpload_UploadFailed"), LanguageManager.Instance.GetString("MessageTitle_OmaeUpload_UploadFailed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
			}
			catch (EndpointNotFoundException)
			{
				MessageBox.Show(NO_CONNECTION_MESSAGE, NO_CONNECTION_TITLE, MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
			objService.Close();
			cmdUpload.Enabled = true;
			txtDescription.Enabled = true;
			txtName.Enabled = true;

			if (blnSuccess)
				this.DialogResult = DialogResult.OK;

			//_objOmaeHelper.DecompressMultiple(bytFile);
		}
		#endregion

		#region Methods
		private void MoveControls()
		{
			int intWidth = Math.Max(lblDescriptionLabel.Width, lblNameLabel.Width);

			txtName.Left = lblNameLabel.Left + intWidth + 6;
			txtName.Width = this.Width - txtName.Left - 16;
			txtDescription.Left = lblDescriptionLabel.Left + intWidth + 6;
			txtDescription.Width = this.Width - txtDescription.Left - 16;
		}
		#endregion
	}
}