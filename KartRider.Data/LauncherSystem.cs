using System;
using System.Windows.Forms;

namespace KartRider
{
	public static class LauncherSystem
	{
		public static void MessageBoxType1()
		{
			//MessageBox.Show("카트라이더가 이미 실행 중입니다. 다시 시도해 주세요!", "오류", MessageBoxButtons.OK, MessageBoxIcon.Information);
			System.Diagnostics.Process[] process = System.Diagnostics.Process.GetProcessesByName("KartRider");
			foreach (System.Diagnostics.Process p in process)
			{
				p.Kill();
			}
		}

		public static void MessageBoxType2()
		{
			MessageBox.Show("카트라이더가 이미 실행 중입니다. 다시 시도해 주세요!", "오류", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		public static void MessageBoxType3()
		{
			MessageBox.Show(Launcher.KartRider + " 또는 " + Launcher.pinFile + " 파일을 찾을 수 없습니다!", "오류", MessageBoxButtons.OK, MessageBoxIcon.Information);
			Environment.Exit(1);
		}
	}
}