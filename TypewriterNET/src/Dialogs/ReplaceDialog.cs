using System;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
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

public class ReplaceDialog : ADialog
{
	public class Data
	{
		public string oldText = "";
		public string oldReplaceText = "";
		public readonly StringList history;
		public readonly StringList replaceHistory;

		public Data(StringList history, StringList replaceHistory)
		{
			this.history = history;
			this.replaceHistory = replaceHistory;
		}
	}

	private Data data;
	private FindParams findParams;
	private Getter<string, bool> doFindText;
	private Getter<string, bool> doSelectAllFound;
	private Getter<string, bool> doSelectNextFound;
	private TabBar<NamedAction> tabBar;
	private MulticaretTextBox textBox;
	private MulticaretTextBox replaceTextBox;
	private MonospaceLabel textLabel;
	private MonospaceLabel replaceTextLabel;

	public ReplaceDialog(Data data, FindParams findParams,
		Getter<string, bool> doFindText,
		Getter<string, bool> doSelectAllFound,
		Getter<string, bool> doSelectNextFound,
		string name)
	{
		this.data = data;
		this.findParams = findParams;
		this.doFindText = doFindText;
		this.doSelectAllFound = doSelectAllFound;
		this.doSelectNextFound = doSelectNextFound;
		Name = name;
	}

	override protected void DoCreate()
	{
		KeyMap frameKeyMap = new KeyMap();
		frameKeyMap.AddItem(new KeyItem(Keys.Escape, null, new KeyAction("F&ind\\Cancel find", DoCancel, null, false)));
		frameKeyMap.AddItem(new KeyItem(Keys.Tab, null, new KeyAction("F&ind\\Next field", DoNextField, null, false)));
		frameKeyMap.AddItem(new KeyItem(Keys.Control | Keys.Tab, null, new KeyAction("F&ind\\Prev field", DoPrevField, null, false)));
		frameKeyMap.AddItem(new KeyItem(Keys.Enter, null, new KeyAction("F&ind\\Find next", DoFind, null, false)));
		frameKeyMap.AddItem(new KeyItem(Keys.Control | Keys.Shift | Keys.H, null, new KeyAction("F&ind\\Replace", DoReplace, null, false)));
		frameKeyMap.AddItem(new KeyItem(Keys.Control | Keys.Alt | Keys.Enter, null, new KeyAction("F&ind\\Replace all", DoReplaceAll, null, false)));
		frameKeyMap.AddItem(new KeyItem(Keys.Up, null, new KeyAction("F&ind\\Previous pattern", DoPrevPattern, null, false)));
		frameKeyMap.AddItem(new KeyItem(Keys.Down, null, new KeyAction("F&ind\\Next pattern", DoNextPattern, null, false)));
		
		KeyMap beforeKeyMap = new KeyMap();
		beforeKeyMap.AddItem(new KeyItem(Keys.Control | Keys.Shift | Keys.D, null,
			new KeyAction("F&ind\\Select all found", DoSelectAllFound, null, false)));
		beforeKeyMap.AddItem(new KeyItem(Keys.Control | Keys.D, null,
			new KeyAction("F&ind\\Select next found", DoSelectNextFound, null, false)));
		
		SwitchList<NamedAction> list = new SwitchList<NamedAction>();
		list.Add(new NamedAction("FIND NEXT", DoFind));
		list.Add(new NamedAction("REPLACE", DoReplace));
		list.Add(new NamedAction("REPLACE ALL", DoReplaceAll));
		list.Add(new NamedAction("SELECT NEXT FOUND", DoSelectNextFound));
		list.Add(new NamedAction("SELECT ALL FOUND", DoSelectAllFound));
		
		tabBar = new TabBar<NamedAction>(list, TabBar<NamedAction>.DefaultStringOf);
		tabBar.Text = Name;
		tabBar.ButtonMode = true;
		tabBar.TabClick += OnTabClick;
		tabBar.CloseClick += OnCloseClick;
		Controls.Add(tabBar);

		textBox = new MulticaretTextBox();
		textBox.ShowLineNumbers = false;
		textBox.HighlightCurrentLine = false;
		textBox.KeyMap.AddAfter(KeyMap);
		textBox.KeyMap.AddBefore(beforeKeyMap);
		textBox.KeyMap.AddAfter(frameKeyMap, 1);
		textBox.KeyMap.AddAfter(DoNothingKeyMap, -1);
		textBox.FocusedChange += OnTextBoxFocusedChange;
		Controls.Add(textBox);

		replaceTextBox = new MulticaretTextBox();
		replaceTextBox.ShowLineNumbers = false;
		replaceTextBox.HighlightCurrentLine = false;
		replaceTextBox.KeyMap.AddAfter(frameKeyMap, 1);
		replaceTextBox.FocusedChange += OnTextBoxFocusedChange;
		Controls.Add(replaceTextBox);

		textLabel = new MonospaceLabel();
		textLabel.Text = "Text";
		Controls.Add(textLabel);

		replaceTextLabel = new MonospaceLabel();
		replaceTextLabel.Text = "Replace";
		Controls.Add(replaceTextLabel);

		tabBar.MouseDown += OnTabBarMouseDown;
		InitResizing(tabBar, null);
		Height = MinSize.Height;
		UpdateFindParams();
	}

	private void UpdateFindParams()
	{
		tabBar.Text2 = findParams.GetIndicationTextWithEscape();
	}

	private void OnCloseClick()
	{
		DispatchNeedClose();
	}
	
	private void OnTabClick(NamedAction action)
	{
		action.Execute(textBox.Controller);
	}

	override protected void DoDestroy()
	{
		data.oldText = textBox.Text;
		data.oldReplaceText = replaceTextBox.Text;
	}

	override public void Focus()
	{
		textBox.Focus();

		Controller lastController = Nest.MainForm.LastFrame != null ? Nest.MainForm.LastFrame.Controller : null;

		textBox.Text = lastController == null || lastController.Lines.LastSelection.Empty ?
			data.oldText :
			lastController.Lines.GetText(lastController.Lines.LastSelection.Left, lastController.Lines.LastSelection.Count);
		textBox.Controller.SelectAllToEnd();

		replaceTextBox.Text = data.oldReplaceText;
		replaceTextBox.Controller.SelectAllToEnd();
	}

	private bool DoCancel(Controller controller)
	{
		DispatchNeedClose();
		return true;
	}

	private bool DoNextField(Controller controller)
	{
		if (controller == textBox.Controller)
			replaceTextBox.Focus();
		else
			textBox.Focus();
		return true;
	}
	
	private bool DoPrevField(Controller controller)
	{
		return DoNextField(controller);
	}

	private bool DoFind(Controller controller)
	{
		string text = textBox.Text;
		if (text != "")
		{
			doFindText(text);
			data.history.Add(text);
		}
		return true;
	}

	private string GetReplaceText()
	{
		if (findParams.escape)
			return Regex.Unescape(replaceTextBox.Text);
		return replaceTextBox.Text;
	}

	private bool DoReplace(Controller controller)
	{
		string text = textBox.Text;
		if (text != "" && Nest.MainForm.LastFrame != null)
		{
			Controller lastController = Nest.MainForm.LastFrame.Controller;
			if (!lastController.Lines.AllSelectionsEmpty)
				lastController.InsertText(GetReplaceText());
			doFindText(text);
			data.history.Add(text);
		}
		return true;
	}

	private bool DoReplaceAll(Controller controller)
	{
		string text = textBox.Text;
		if (text != "" && Nest.MainForm.LastFrame != null)
		{
			Controller lastController = Nest.MainForm.LastFrame.Controller;
			lastController.ClearMinorSelections();
			lastController.LastSelection.anchor = lastController.LastSelection.caret = 0;
			while (true)
			{
				doFindText(text);
				if (!lastController.Lines.AllSelectionsEmpty)
					lastController.InsertText(GetReplaceText());
				else
					break;
			}
			data.history.Add(text);
		}
		return true;
	}

	override public Size MinSize
	{
		get { return new Size(tabBar.Height * 3, tabBar.Height + textBox.CharHeight * 2 + 4); }
	}

	private void OnTabBarMouseDown(object sender, EventArgs e)
	{
		textBox.Focus();
	}

	private void OnTextBoxFocusedChange()
	{
		if (Destroyed)
			return;
		tabBar.Selected = textBox.Focused || replaceTextBox.Focused;
		if (textBox.Focused)
			Nest.MainForm.SetFocus(textBox, textBox.KeyMap, null);
		if (replaceTextBox.Focused)
			Nest.MainForm.SetFocus(replaceTextBox, textBox.KeyMap, null);
	}

	override public bool Focused { get { return textBox.Focused || replaceTextBox.Focused; } }

	override protected void OnResize(EventArgs e)
	{
		base.OnResize(e);
		int tabBarHeight = tabBar.Height;
		tabBar.Size = new Size(Width, tabBarHeight);
		textLabel.Location = new Point(0, tabBarHeight);
		replaceTextLabel.Location = new Point(0, tabBarHeight + (Height - tabBarHeight) / 2 + 2);

		int left = Math.Max(textLabel.Width, replaceTextLabel.Width) + 10;
		textBox.Location = new Point(left, tabBarHeight);
		textBox.Size = new Size(Width - left, (Height - tabBarHeight) / 2);
		replaceTextBox.Location = new Point(left, tabBarHeight + (Height - tabBarHeight) / 2 + 2);
		replaceTextBox.Size = new Size(Width - left, (Height - tabBarHeight) / 2);
	}

	override protected void DoUpdateSettings(Settings settings, UpdatePhase phase)
	{
		if (phase == UpdatePhase.Raw)
		{
			settings.ApplySimpleParameters(textBox, null);
			settings.ApplySimpleParameters(replaceTextBox, null);
			settings.ApplyToLabel(textLabel);
			settings.ApplyToLabel(replaceTextLabel);
		}
		else if (phase == UpdatePhase.Parsed)
		{
			BackColor = settings.ParsedScheme.tabsBg.color;
			textBox.Scheme = settings.ParsedScheme;
			replaceTextBox.Scheme = settings.ParsedScheme;
			tabBar.Scheme = settings.ParsedScheme;
			settings.ApplySchemeToLabel(textLabel);
			settings.ApplySchemeToLabel(replaceTextLabel);
		}
		else if (phase == UpdatePhase.FindParams)
		{
			UpdateFindParams();
		}
	}

	private bool DoPrevPattern(Controller controller)
	{
		return GetHistoryPattern(controller, true);
	}

	private bool DoNextPattern(Controller controller)
	{
		return GetHistoryPattern(controller, false);
	}

	private bool GetHistoryPattern(Controller controller, bool isPrev)
	{
		string text;
		string newText;
		MulticaretTextBox currentTextBox;
		if (controller == textBox.Controller)
		{
			currentTextBox = textBox;
			text = currentTextBox.Text;
			newText = data.history.Get(text, isPrev);
		}
		else
		{
			currentTextBox = replaceTextBox;
			text = currentTextBox.Text;
			newText = data.replaceHistory.Get(text, isPrev);
		}
		if (newText != text)
		{
			currentTextBox.Text = newText;
			currentTextBox.Controller.ClearMinorSelections();
			currentTextBox.Controller.LastSelection.anchor = currentTextBox.Controller.LastSelection.caret = newText.Length;
			return true;
		}
		return false;
	}
	
	private bool DoSelectAllFound(Controller controller)
	{
		string text = textBox.Text;
		if (data.history != null)
			data.history.Add(text);
		if (doSelectAllFound(text))
			DispatchNeedClose();
		return true;
	}
	
	private bool DoSelectNextFound(Controller controller)
	{
		string text = textBox.Text;
		if (data.history != null)
			data.history.Add(text);
		doSelectNextFound(text);
		return true;
	}
}
