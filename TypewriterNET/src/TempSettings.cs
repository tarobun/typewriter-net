using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using MulticaretEditor;

public class TempSettings
{
	private MainForm mainForm;
	private Settings settings;
	private FileQualitiesStorage storage = new FileQualitiesStorage();

	public TempSettings(MainForm mainForm, Settings settings)
	{
		this.mainForm = mainForm;
		this.settings = settings;
	}

	private static string GetTempSettingsPath()
	{
		return Path.Combine(Path.GetTempPath(), "typewriter-state.bin");
	}
	
	public void Load()
	{
		SValue state = SValue.None;
		string file = GetTempSettingsPath();
		if (File.Exists(file))
			state = SValue.Unserialize(File.ReadAllBytes(file));
		
		mainForm.Size = new Size(state["width"].GetInt(700), state["height"].GetInt(480));
		mainForm.Location = new Point(state["x"].Int, state["y"].Int);
		mainForm.WindowState = state["maximized"].GetBool(false) ? FormWindowState.Maximized : FormWindowState.Normal;
		storage.Unserialize(state["storage"]);
		if (settings.rememberOpenedFiles.Value)
		{
			foreach (SValue valueI in state["openedTabs"].List)
			{
				string fullPath = valueI["fullPath"].String;
				if (fullPath != "" && File.Exists(fullPath))
					mainForm.LoadFile(fullPath);
			}
			Buffer selectedTab = mainForm.MainNest.buffers.GetByFullPath(BufferTag.File, state["selectedTab"]["fullPath"].String);
			if (selectedTab != null)
				mainForm.MainNest.buffers.list.Selected = selectedTab;
		}
	}

	public void StorageQualities(Buffer buffer)
	{
		storage.Set(buffer.FullPath).With("cursor", SValue.NewInt(buffer.Controller.Lines.LastSelection.caret));
	}

	public void ApplyQualities(Buffer buffer)
	{
		int caret = storage.Get(buffer.FullPath)["cursor"].Int;
		buffer.Controller.PutCursor(buffer.Controller.SoftNormalizedPlaceOf(caret), false);
		buffer.Controller.NeedScrollToCaret();
	}

	public void Save()
	{
		SValue state = SValue.NewHash();
		if (mainForm.WindowState == FormWindowState.Maximized)
		{
			state["width"] = SValue.NewInt(mainForm.RestoreBounds.Width);
			state["height"] = SValue.NewInt(mainForm.RestoreBounds.Height);
			state["x"] = SValue.NewInt(mainForm.RestoreBounds.X);
			state["y"] = SValue.NewInt(mainForm.RestoreBounds.Y);
		}
		else
		{
			state["width"] = SValue.NewInt(mainForm.Width);
			state["height"] = SValue.NewInt(mainForm.Height);
			state["x"] = SValue.NewInt(mainForm.Location.X);
			state["y"] = SValue.NewInt(mainForm.Location.Y);
		}
		state["maximized"] = SValue.NewBool(mainForm.WindowState == FormWindowState.Maximized);
		if (settings.rememberOpenedFiles.Value)
		{
			SValue openedTabs = state.SetNewList("openedTabs");
			foreach (Buffer buffer in mainForm.MainNest.buffers.list)
			{
				SValue valueI = SValue.NewHash().With("fullPath", SValue.NewString(buffer.FullPath));
				openedTabs.Add(valueI);
				if (buffer == mainForm.MainNest.buffers.list.Selected)
					state["selectedTab"] = valueI;
			}
		}
		state["storage"] = storage.Serialize();
		
		File.WriteAllBytes(GetTempSettingsPath(), SValue.Serialize(state));
	}
}