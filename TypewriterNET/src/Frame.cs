using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Design;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Text;
using System.Diagnostics;
using Microsoft.Win32;
using MulticaretEditor.KeyMapping;
using MulticaretEditor.Highlighting;
using MulticaretEditor;

public class Frame : AFrame
{
	private static Controller _emptyController;

	private static Controller GetEmptyController()
	{
		if (_emptyController == null)
		{
			_emptyController = new Controller(new LineArray());
			_emptyController.InitText("[Empty]");
			_emptyController.isReadonly = true;
		}
		return _emptyController;
	}

	private TabBar<Buffer> tabBar;
	private SplitLine splitLine;
	private MulticaretTextBox textBox;
	private BufferList buffers;

	override protected void DoCreate()
	{
		if (Nest.buffers == null)
			throw new Exception("buffers == null");
		this.buffers = Nest.buffers;

		buffers.frame = this;
		buffers.list.SelectedChange += OnTabSelected;

		tabBar = new TabBar<Buffer>(buffers.list, Buffer.StringOf);
		tabBar.CloseClick += OnCloseClick;
		tabBar.TabDoubleClick += OnTabDoubleClick;
		Controls.Add(tabBar);
		splitLine = new SplitLine();
		Controls.Add(splitLine);

		KeyMap frameKeyMap = new KeyMap();
		frameKeyMap.AddItem(new KeyItem(Keys.Tab, Keys.Control, new KeyAction("&View\\Switch tab", DoTabDown, DoTabModeChange, false)));
		frameKeyMap.AddItem(new KeyItem(Keys.Control | Keys.W, null, new KeyAction("&View\\Close tab", DoCloseTab, null, false)));

		textBox = new MulticaretTextBox();
		textBox.KeyMap.AddAfter(KeyMap);
		textBox.KeyMap.AddAfter(frameKeyMap);
		textBox.KeyMap.AddAfter(DoNothingKeyMap, -1);
		textBox.FocusedChange += OnTextBoxFocusedChange;
		textBox.Controller = GetEmptyController();
		Controls.Add(textBox);

		InitResizing(tabBar, splitLine);
		tabBar.MouseDown += OnTabBarMouseDown;
		OnTabSelected();
	}

	override protected void DoDestroy()
	{
		tabBar.List = null;
		buffers.list.SelectedChange -= OnTabSelected;
		buffers.frame = null;
	}

	public MulticaretTextBox TextBox { get { return textBox; } }
	public Controller Controller { get { return textBox.Controller; } }

	private bool DoTabDown(Controller controller)
	{
		buffers.list.Down();
		return true;
	}

	private void DoTabModeChange(Controller controller, bool mode)
	{
		if (mode)
			buffers.list.ModeOn();
		else
			buffers.list.ModeOff();
	}

	private bool DoCloseTab(Controller controller)
	{
		RemoveBuffer(buffers.list.Selected);
		return true;
	}

	override public Size MinSize { get { return new Size(tabBar.Height * 3, tabBar.Height); } }
	override public Frame AsFrame { get { return this; } }

	public Buffer SelectedBuffer
	{
		get { return buffers.list.Selected; }
		set
		{
			if (value.Frame == this)
				buffers.list.Selected = value;
		}
	}

	override public bool Focused { get { return textBox.Focused; } }

	override public void Focus()
	{
		textBox.Focus();
	}

	private void OnTabBarMouseDown(object sender, EventArgs e)
	{
		textBox.Focus();
	}

	private void OnTextBoxFocusedChange()
	{
		if (Nest != null)
		{
			tabBar.Selected = textBox.Focused;
			Nest.MainForm.SetFocus(textBox, textBox.KeyMap, this);
		}
	}

	public string Title
	{
		get { return tabBar.Text; }
		set { tabBar.Text = value; }
	}

	override protected void OnResize(EventArgs e)
	{
		base.OnResize(e);
		int tabBarHeight = tabBar.Height;
		tabBar.Size = new Size(Width, tabBarHeight);
		splitLine.Location = new Point(Width - 10, tabBarHeight);
		splitLine.Size = new Size(10, Height - tabBarHeight);
		textBox.Location = new Point(0, tabBarHeight);
		textBox.Size = new Size(Width - 10, Height - tabBarHeight);
	}

	private Settings settings;

	override protected void DoUpdateSettings(Settings settings, UpdatePhase phase)
	{
		this.settings = settings;
		Buffer buffer = buffers.list.Selected;

		if (phase == UpdatePhase.Raw)
		{
			settings.ApplyParameters(textBox, buffer != null ? buffer.settingsMode : SettingsMode.None);
			tabBar.SetFont(settings.font.Value, settings.fontSize.Value);
		}
		else if (phase == UpdatePhase.Parsed)
		{
			textBox.Scheme = settings.ParsedScheme;
			tabBar.Scheme = settings.ParsedScheme;
			if (settings.showEncoding.Value)
				tabBar.Text2Of = Buffer.EncodeOf;
			else
				tabBar.Text2Of = null;
			splitLine.Scheme = settings.ParsedScheme;
		}
		else if (phase == UpdatePhase.HighlighterChange)
		{
			UpdateHighlighter();
		}

		if (buffer != null && buffer.onUpdateSettings != null)
			buffer.onUpdateSettings(buffer, phase);
	}

	public bool ContainsBuffer(Buffer buffer)
	{
		return buffers.list.Contains(buffer);
	}

	public Buffer GetBuffer(string fullPath, string name)
	{
		return buffers.GetBuffer(fullPath, name);
	}

	public void AddBuffer(Buffer buffer)
	{
		if (buffer.Frame != this)
		{
			if (buffer.Frame != null)
			{
				buffer.softRemove = true;
				buffer.Frame.RemoveBuffer(buffer);
				buffer.softRemove = false;
			}
			buffer.owner = buffers;
			buffer.Controller.history.ChangedChange += OnChangedChange;
			buffers.list.Add(buffer);
			if (buffer.onAdd != null)
				buffer.onAdd(buffer);
		}
		else
		{
			buffers.list.Add(buffer);
		}
	}

	public void RemoveBuffer(Buffer buffer)
	{
		if (buffer == null)
			return;
		if (!buffer.softRemove && buffer.onRemove != null && !buffer.onRemove(buffer))
			return;
		buffer.Controller.history.ChangedChange -= OnChangedChange;
		buffer.owner = null;
		buffers.list.Remove(buffer);
		if (buffers.list.Count == 0)
			Destroy();
	}

	private void OnChangedChange()
	{
		tabBar.Invalidate();
	}

	private KeyMap additionKeyMap;
	private KeyMap additionBeforeKeyMap;

	private void OnTabSelected()
	{
		Buffer buffer = buffers.list.Selected;
		if (additionKeyMap != null)
			textBox.KeyMap.RemoveAfter(additionKeyMap);
		if (additionBeforeKeyMap != null)
			textBox.KeyMap.RemoveBefore(additionBeforeKeyMap);
		additionKeyMap = buffer != null ? buffer.additionKeyMap : null;
		additionBeforeKeyMap = buffer != null ? buffer.additionBeforeKeyMap : null;
		textBox.Controller = buffer != null ? buffer.Controller : GetEmptyController();
		if (additionKeyMap != null)
			textBox.KeyMap.AddAfter(additionKeyMap, 1);
		if (additionBeforeKeyMap != null)
			textBox.KeyMap.AddBefore(additionBeforeKeyMap);
		if (settings != null && buffer != null)
			settings.ApplyParameters(textBox, buffer.settingsMode);
		UpdateHighlighter();
		if (buffer != null && buffer.onSelected != null)
			buffer.onSelected(buffer);
		if (Nest != null)
			Nest.MainForm.UpdateTitle();
	}

	public void UpdateHighlighter()
	{
		Buffer buffer = buffers.list.Selected != null ? buffers.list.Selected : null;
		Nest.MainForm.UpdateHighlighter(textBox, buffer != null ? buffer.Name : null, buffer);
	}

	private void OnCloseClick()
	{
		RemoveBuffer(buffers.list.Selected);
	}

	private void OnTabDoubleClick(Buffer buffer)
	{
		RemoveBuffer(buffer);
	}
	
	public void ShowAutocomplete(List<string> variants)
	{
		Buffer buffer = buffers.list.Selected != null ? buffers.list.Selected : null;
		if (buffer == null)
			return;
		
		Place place = textBox.Controller.Lines.PlaceOf(textBox.Controller.LastSelection.caret);
		Point point = textBox.ScreenCoordsOfPlace(place);
		point.Y += textBox.CharHeight;
		
		ToolStripDropDown dropDown = new ToolStripDropDown();
		ToolStripItem[] items = new ToolStripItem[variants.Count];
		for (int i = 0; i < variants.Count; i++)
		{
			ToolStripButton button = new ToolStripButton();
			button.Text = variants[i];
			items[i] = button;
		}
		dropDown.Items.AddRange(items);
		dropDown.Show(textBox, point);
	}
}
