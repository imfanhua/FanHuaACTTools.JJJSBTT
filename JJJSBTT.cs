using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Advanced_Combat_Tracker;

namespace FanHuaACTTools.JJJSBTT {

	public partial class JJJSBTT: UserControl, IActPluginV1 {

		private readonly TextBox[] _textBoxTargets;
		private readonly TextBox[] _textBoxTeams;
		private readonly RadioButton[] _radioButtonMe;
		private bool _updatingUI;

		public JJJSBTT() {
			InitializeComponent();
			Dock = DockStyle.Fill;

			_textBoxTargets = new TextBox[] { textBoxName1, textBoxName2, textBoxName3, textBoxName4, textBoxName5, textBoxName6, textBoxName7, textBoxName8 };
			_textBoxTeams = new TextBox[] { textBoxTeam1, textBoxTeam2, textBoxTeam3, textBoxTeam4, textBoxTeam5, textBoxTeam6, textBoxTeam7, textBoxTeam8 };
			_radioButtonMe = new RadioButton[] { radioButtonMe1, radioButtonMe2, radioButtonMe3, radioButtonMe4, radioButtonMe5, radioButtonMe6, radioButtonMe7, radioButtonMe8 };
		}

		private void LoadUI(string[] targets, string[] teams, int me, bool tts, bool autoTag) {
			_updatingUI = true;
			_me = me;
			_tts = tts;
			_autoTag = autoTag;
			checkBoxTTS.Checked = tts;
			checkBoxAutoTag.Checked = autoTag;
			for (var i = 0; i < 8; i++) {
				_targets[i] = targets[i];
				_textBoxTargets[i].Text = targets[i];
				_textBoxTeams[i].Text = teams[i];
				_radioButtonMe[i].Checked = i == me;
			}
			_updatingUI = false;
			
			for (var i = 0; i < 8; i++) {
				_textBoxTeams[i].Visible = autoTag;
				_radioButtonMe[i].Visible = tts;
				UpdateTargetTeam(i);
			}
			CheckAll();
		}

		private string SettingsFile => Path.Combine(ActGlobals.oFormActMain.AppDataFolder.FullName, "FanHua.JJJSBTT.Settings.bin");

		private void LoadSettings() {
			try {
				if (File.Exists(SettingsFile)) {
					var data = File.ReadAllText(SettingsFile).Split(':');
					LoadUI(data[0].Split('|'), data[1].Split('|'), int.Parse(data[2]), data[3] == "1", data[4] == "1");
					return;
				}
			} catch {}
			LoadUI(new string[] { "", "", "", "", "", "", "", "" }, new string[] { "1", "2", "3", "4", "5", "6", "7", "8" }, 0, true, false);
		}

		private void SaveSettings() {
			var data =
				string.Join("|", _textBoxTargets.Select(box => box.Text?.Trim() ?? "")) + ":" +
				string.Join("|", _textBoxTeams.Select(box => box.Text?.Trim() ?? "")) + ":" +
				_me + ":" + (checkBoxTTS.Checked ? "1" : "0") + ":" + (checkBoxAutoTag.Checked ? "1" : "0");
			File.WriteAllText(SettingsFile, data);
		}
		
		private static Label _labelStatus;

		private bool _setup = false;
		private readonly string[] _targets = new string[8];
		private readonly int[] _teams = new int[8];
		private int _me;
		private bool _tts;
		private bool _autoTag;

		private DateTime _time;
		private byte _size;
		private readonly int[] _array = new int[3];
		
		public void InitPlugin(TabPage page, Label status) {
			_labelStatus = status;
			ActGlobals.oFormActMain.OnLogLineRead += OnLogLineRead;
			status.Text = "载入成功...";
			page.Text = "繁华的绝神兵泰坦工具";
			LoadSettings();
			page.Controls.Add(this);
		}

		public void DeInitPlugin() {
			ActGlobals.oFormActMain.OnLogLineRead -= OnLogLineRead;
			_labelStatus.Text = "已停止...";
			_labelStatus = null;

			SaveSettings();
		}

		private void OnLogLineRead(bool isImport, LogLineEventArgs log) {
			if (isImport) return;
			var line = log.logLine;
			if (line == null) return;
			
			var index = line.IndexOf("JJJSBTT {");
			if (index != -1) {
				var after = line.Substring(index + "JJJSBTT {".Length);
				index = after.IndexOf("}");
				if (index != -1) {
					SetupTargets(after.Substring(0, index).Split(':'));
					return;
				}
			}

			if (!_setup) return;
			if (line.Contains(":2B6B:") || line.Contains(":2B6C:")) {
				for (var i = 0; i < 8; i++)
					if (line.Contains($":{_targets[i]}:")) {
						DoJJJSB(i);
						return;
					}
			}
		}
		
		private void SetupTargets(string[] data) => Invoke(new Action(() => SetAllTargets(data)));

		private void SetAllTargets(string[] data) {
			if (data.Length != 8) return;
			_updatingUI = false;
			for (var i = 0; i < 8; i++) {
				var name = data[i]?.Trim() ?? "";
				_targets[i] = name;
				_teams[i] = i;

				_textBoxTargets[i].Text = name;
				_textBoxTeams[i].Text = (i + 1).ToString();
			}
			_updatingUI = true;
			CheckAll();
		}

		private void CheckAll() {
			if (!CheckTargets()) {
				_setup = false;
				labelSetupStatus.Text = "名字请勿留空…";
				labelSetupStatus.ForeColor = Color.Red;
				return;
			}

			if (_autoTag) {
				if (!CheckTeams()) {
					_setup = false;
					labelSetupStatus.Text = "队伍顺序有冲突…";
					labelSetupStatus.ForeColor = Color.Red;
					return;
				}
			}

			_setup = true;
			labelSetupStatus.Text = "可以使用。";
			labelSetupStatus.ForeColor = Color.Green;
		}

		private bool CheckTargets() {
			for (var i = 0; i < 8; i++) {
				var target = _targets[i];
				if (target == null || target.Length < 1) return false;
			}

			return true;
		}

		private bool CheckTeams() {
			var list = new List<int>(8);
			for (var i = 0; i < 8; i++) {
				var team = _teams[i];
				if (list.Contains(team)) return false;
				list.Add(team);
			}

			return true;
		}
		
		private void DoJJJSB(int who) {
			var time = DateTime.Now;
			if ((time - _time).TotalMilliseconds > 1500) {
				_size = 0;
				_time = time;
			}
			
			_array[_size++] = who;

			if (_size >= 3) {
				_size = 0;
				var list = _array.OrderBy(value => value).ToList();
				RunJJJSB(list[0], list[1], list[2]);
			}
		}

		private void RunJJJSB(int t1, int t2, int t3) {
			if (_autoTag) new Tagger(_teams[t1], _teams[t2], _teams[t3]).Start();
			if (_tts) {
				if (t1 == _me) TTS("石牢第一");
				else if (t2 == _me) TTS("石牢第二");
				else if (t3 == _me) TTS("石牢第三");
			}
		}

		private void TTS(string thing) => ActGlobals.oFormActMain.TTS(thing);

		private void Clear(object sender, EventArgs args) {
			if (MessageBox.Show("真的要清空吗？", "询问：", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
			_updatingUI = true;
			for (var i = 0; i < 8; i++) {
				_targets[i] = "";
				_teams[i] = i;
				_textBoxTargets[i].Text = "";
				_textBoxTeams[i].Text = (i + 1).ToString();
			}
			_updatingUI = false;

			CheckAll();
		}

		private void TestRandom(object sender, EventArgs args) {
			if (!_setup) return;
			var list = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
			var random = new Random();
			var i = random.Next(list.Count);
			var value = list[i];
			list.RemoveAt(i);
			DoJJJSB(value);
			i = random.Next(list.Count);
			value = list[i];
			list.RemoveAt(i);
			DoJJJSB(value);
			i = random.Next(list.Count);
			value = list[i];
			list.RemoveAt(i);
			DoJJJSB(value);
		}

		private void Test123(object sender, EventArgs args) {
			if (!_setup) return;
			DoJJJSB(1);
			DoJJJSB(0);
			DoJJJSB(2);
		}

		private void SwitchData(int index, int to) {
			_updatingUI = true;
			if (to < 0) to = 7;
			if (to > 7) to = 0;
			var name = _textBoxTargets[index].Text;
			var team = _textBoxTeams[index].Text;
			var me = _radioButtonMe[index].Checked;
			_textBoxTargets[index].Text = _textBoxTargets[to].Text;
			_textBoxTeams[index].Text = _textBoxTeams[to].Text;
			_radioButtonMe[index].Checked = _radioButtonMe[to].Checked;
			_textBoxTargets[to].Text = name;
			_textBoxTeams[to].Text = team;
			_radioButtonMe[to].Checked = me;
			_updatingUI = false;

			for (var i = 0; i < 8; i++)
				if (_radioButtonMe[i].Checked) {
					_me = i;
					break;
				}

			_targets[index] = _textBoxTargets[index].Text?.Trim();
			_targets[to] = _textBoxTargets[to].Text?.Trim();
			UpdateTargetTeam(index);
			UpdateTargetTeam(to);
			CheckAll();
		}

		private void UpdateTargetTeam(int index) {
			var box = _textBoxTeams[index];
			if (!int.TryParse(box.Text, out var team)) team = -1;

			if (team < 1 || team > 8) {
				_updatingUI = true;
				box.Text = "1";
				team = 1;
				_updatingUI = false;
			}

			_teams[index] = team - 1;
		}

		private void MoveUp(object sender, EventArgs args) {
			var index = int.Parse((string) ((Button) sender).Tag);
			SwitchData(index, index - 1);
		}

		private void MoveDown(object sender, EventArgs args) {
			var index = int.Parse((string) ((Button) sender).Tag);
			SwitchData(index, index + 1);
		}

		private void OnMeButtonCheckedChanged(object sender, EventArgs args) {
			if (_updatingUI) return;
			if (!((RadioButton) sender).Checked) return;
			_updatingUI = true;
			var index = int.Parse((string) ((RadioButton) sender).Tag);
			_me = index;
			for (var i = 0; i < 8; i++) _radioButtonMe[i].Checked = i == index;
			_updatingUI = false;
		}

		private void OnTTSCheckedChanged(object sender, EventArgs args) {
			_tts = checkBoxTTS.Checked;
			for (var i = 0; i < 8; i++) _radioButtonMe[i].Visible = _tts;
			CheckAll();
		}

		private void OnAutoTagCheckedChanged(object sender, EventArgs args) {
			_autoTag = checkBoxAutoTag.Checked;
			for (var i = 0; i < 8; i++) _textBoxTeams[i].Visible = _autoTag;
			CheckAll();
		}

		private void OnTargetTextChanged(object sender, EventArgs args) {
			if (_updatingUI) return;
			var box = (TextBox) sender;
			_targets[int.Parse((string) box.Tag)] = box.Text?.Trim();
			CheckAll();
		}

		private void OnTargetTeamChanged(object sender, EventArgs args) {
			if (_updatingUI) return;
			UpdateTargetTeam(int.Parse((string) ((TextBox) sender).Tag));
			CheckAll();
		}

	}

	class Tagger {

		private static Keys TargetToKeys(int target) {
			switch (target) {
				case 0: return Keys.F5;
				case 1: return Keys.F6;
				case 2: return Keys.F7;
				case 3: return Keys.F8;
				case 4: return Keys.F9;
				case 5: return Keys.F10;
				case 6: return Keys.F11;
				case 7: return Keys.F12;
				default: return Keys.F13;
			}
		}

		private readonly Keys _t1;
		private readonly Keys _t2;
		private readonly Keys _t3;

		public Tagger(int t1, int t2, int t3) {
			_t1 = TargetToKeys(t1);
			_t2 = TargetToKeys(t2);
			_t3 = TargetToKeys(t3);
		}

		public void Start() => new Thread(Run) { IsBackground = true }.Start();

		private void Run() {
			try {
				var handle = Natives.Find("最终幻想XIV");
				if (handle == IntPtr.Zero)
					return;
				Natives.SendKey(handle, _t1);
				Natives.SendKey(handle, _t2);
				Natives.SendKey(handle, _t3);
			} catch {}
		}

	}

	static class Natives {

		[DllImport("user32.dll", EntryPoint = "PostMessageA", SetLastError = true)]
		private static extern int PostMessage(IntPtr handle, int msg, int param, int arg);
		
		[DllImport("user32.dll", EntryPoint = "FindWindow", SetLastError = true)]
		private static extern IntPtr FindWindow(string className, string windowName);

		public static IntPtr Find(string name) => FindWindow(null, "最终幻想XIV");

		public static void SendKey(IntPtr handle, Keys key) {
			PostMessage(handle, WindowsMessages.KeyDown, (int) key, 0);
			PostMessage(handle, WindowsMessages.KeyUp, (int) key, 0);
			Thread.Sleep(100);
		}

	}

	static class WindowsMessages {
		public const int KeyDown = 256;
		public const int KeyUp = 257;
	}

}
