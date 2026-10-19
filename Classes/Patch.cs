/*
    @app        : StartAllBack Activator
    @repo       : https://github.com/Aetherinox/startallback-utility
    @author     : Aetherinox
*/

using System;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Text;
using Lng = SABActivator.Properties.Resources;
using Cfg = SABActivator.Properties.Settings;

namespace SABActivator
{
    class Patch
    {

        private static string path_exe          = System.IO.Path.GetDirectoryName( System.Reflection.Assembly.GetEntryAssembly( ).Location );
        private static string dll_target        = Cfg.Default.app_target_dll;

        private static string dll_appdata       = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ),
                                                    "StartAllBack",
                                                    dll_target
                                                );

        private static string dll_prog64        = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.ProgramFiles ),
                                                    "StartAllBack",
                                                    dll_target
                                                );

        private static string dll_prog86        = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.ProgramFilesX86 ),
                                                    "StartAllBack",
                                                    dll_target
                                                );

        private static string dll_exedir        = Path.Combine( path_exe,
                                                    "StartAllBack",
                                                    dll_target
                                                );

        /*
             Start Patch
        */

        public string Start( string path_selected = "auto" )
        {

            /*
                define arrays
            */

            string[] paths_arr      = new string[] { };
            string[] paths_lst      = new string[] { };

            /*
                populate path list array
            */

            Array.Resize( ref paths_lst, paths_lst.Length + 1 );
            paths_lst [ paths_lst.Length - 1 ] = dll_appdata;

            Array.Resize( ref paths_lst, paths_lst.Length + 1 );
            paths_lst [ paths_lst.Length - 1 ] = dll_prog64;

            Array.Resize( ref paths_lst, paths_lst.Length + 1 );
            paths_lst [ paths_lst.Length - 1 ] = dll_prog86;

            Array.Resize( ref paths_lst, paths_lst.Length + 1 );
            paths_lst [ paths_lst.Length - 1 ] = dll_exedir;

            /*
                define
            */

            if ( File.Exists( dll_appdata ) )
            {
                Array.Resize( ref paths_arr, paths_arr.Length + 1 );
                paths_arr [ paths_arr.Length - 1 ] = dll_appdata;
            }

            if ( File.Exists( dll_prog64 ) )
            {
                Array.Resize( ref paths_arr, paths_arr.Length + 1 );
                paths_arr [ paths_arr.Length - 1 ] = dll_prog64;
            }

            if ( File.Exists( dll_prog86 ) )
            {
                Array.Resize( ref paths_arr, paths_arr.Length + 1 );
                paths_arr [ paths_arr.Length - 1 ] = dll_prog86;
            }

            if ( File.Exists( dll_exedir ) )
            {
                Array.Resize( ref paths_arr, paths_arr.Length + 1 );
                paths_arr [ paths_arr.Length - 1 ] = dll_exedir;
            }

            /*
                custom file loaded
            */

            if ( path_selected != "auto" )
            {

                if ( File.Exists( path_selected ) )
                {

                    paths_arr      = new string[] { };
                    paths_lst      = new string[] { };

                    Array.Resize( ref paths_arr, paths_arr.Length + 1 );
                    paths_arr [ paths_arr.Length - 1 ] = path_selected;

                    Array.Resize( ref paths_lst, paths_lst.Length + 1 );
                    paths_lst [ paths_lst.Length - 1 ] = path_selected;

                    #if DEBUG
                        MessageBox.Show(
                            "DLL file was manually loaded\n\n" + path_selected,
                            "Debug: Custom DLL Loaded",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.None
                        );
                    #endif

                }
            }

            /*
                count results
            */

            int i_arr = paths_arr.Length;

            /*
                if list of paths empty, concat array into string and return error message listing what
                paths were checked.
            */

            if ( i_arr == 0 )
            {

                string path_compiled    = "";
                StringBuilder sb        = new StringBuilder( );

                sb.Append( Environment.NewLine );

                foreach ( string file in paths_lst )
                {
                    sb.Append( Environment.NewLine );
                    sb.Append( file );

                    path_compiled = sb.ToString( );
                }

                MessageBox.Show(
                    string.Format( Lng.msgbox_nolocpath_msg, path_compiled ),
                    Lng.msgbox_nolocpath_title,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );

                return Lng.statusbar_sab_not_found;
            }

            /*
                Temp kill automatic shell restart when process is killed
                AutoRestartShell = 0
            */

            RegistryKey ourKey  = Registry.LocalMachine.OpenSubKey(
                                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon",
                                    true
                                );
            ourKey?.SetValue    ( "AutoRestartShell", 0 );

            bool patched_any    = false;
            bool shell_restored = false;

            /*
                Everything below runs inside try/finally so the shell is ALWAYS
                restored (explorer relaunched + AutoRestartShell re-enabled), even
                on early return or exception. Without this, a failed patch leaves
                the machine with no desktop and shell auto-restart disabled.
            */

            try
            {

            /*
                 kill task explorer.exe
             */

            try
            {
                Process[] processes = Process.GetProcessesByName( "explorer" );
                foreach ( Process proc in processes )
                {
                    proc.Kill( );
                }
            }
            catch ( Exception )
            {
                return Lng.statusbar_taskkill_proc;
            }

            /*
                 kill / restart task explorer.exe
             */

            try
            {
                Process.Start( "cmd", "/c taskkill /f /im explorer.exe" ).WaitForExit( );
            }
            catch ( Exception )
            {
                return Lng.statusbar_taskkill_cmd;
            }

            /*
                 kill task StartAllBackCfg.exe
             */

            try
            {
                Process[] processes = Process.GetProcessesByName( "StartAllBackCfg" );
                foreach ( Process proc in processes )
                {
                    proc.Kill( );
                }
            }
            catch ( Exception )
            {
                return Lng.statusbar_taskkill_startallback;
            }

            /*
                loop each dll path and modify the hex
            */

            foreach ( string path_dll in paths_arr )
            {
                string path_backup      = path_dll + ".bak";

                /*
                    read the current bytes and attempt the signature
                    replacement BEFORE touching anything on disk
                */

                byte[] bytes            = File.ReadAllBytes( path_dll );
                string hex_replace      = BitConverter.ToString( bytes ).Replace( "-", " " );
                string hex_result       = hex_replace.Replace(
                                            @"48 89 5C 24 08 55 56 57 48 8D AC 24 70 FF FF FF",
                                            @"67 C7 01 01 00 00 00 B8 01 00 00 00 C3 90 90 90"
                                        );

                /*
                    signature absent -> the DLL is already patched, or is a
                    version this tool doesn't know. Skip it. We deliberately do
                    NOT restore an old ".bak" over it: that silently downgraded
                    an updated StartAllBack (e.g. v3.9.28 back to a backed-up
                    v3.9.23) and lost the activation.
                */

                if ( hex_result == hex_replace )
                    continue;

                #if DEBUG
                    try
                    {
                        StreamWriter sw_o = new StreamWriter( "hex_original.dmp" );
                        sw_o.WriteLine( hex_replace );
                        sw_o.Close( );

                        StreamWriter sw_p = new StreamWriter( "hex_patched.dmp" );
                        sw_p.WriteLine( hex_result );
                        sw_p.Close( );
                    }
                    catch ( Exception e )
                    {
                        Console.WriteLine( "Debug Hex Dump: Exception: " + e.Message );
                    }
                    finally
                    {
                        Console.WriteLine( "Debug Hex Dump: Finished" );
                    }
                #endif

                string[] hex_patch      = hex_result.Split( ' ' );
                byte[] bytes_modified   = new byte[ hex_patch.Length ];

                for ( int i = 0 ; i < hex_patch.Length ; i++ )
                {
                    bytes_modified [ i ] = Convert.ToByte( hex_patch [ i ], 16 );
                }

                /*
                    back up THIS exact version (replacing any stale backup),
                    then write the patch. The ".bak" always matches the DLL we
                    just patched, so re-running can never downgrade it.
                */

                if ( File.Exists( path_backup ) )
                    File.SetAttributes( path_backup, FileAttributes.Normal );

                File.SetAttributes      ( path_dll, FileAttributes.Normal );
                File.Copy               ( path_dll, path_backup, true );
                File.SetAttributes      ( path_backup, FileAttributes.Normal );

                File.WriteAllBytes      ( path_dll, bytes_modified );
                patched_any = true;

                /*
                    lock the patched DLL so StartAllBack's auto-updater cannot
                    replace it -> activation persists, with NO startup entry.
                    Read/execute stay allowed (the DLL still loads); only write,
                    delete and attribute changes are denied. To let StartAllBack
                    update again, unlock first:
                        takeown /f "<dll>"
                        icacls "<dll>" /remove:d *S-1-1-0
                        attrib -R "<dll>"
                */

                try
                {
                    File.SetAttributes( path_dll, FileAttributes.ReadOnly );

                    using ( PowerShell ps = PowerShell.Create( ) )
                    {
                        ps.AddScript( "icacls '" + path_dll + "' /deny *S-1-1-0:(DE,WD,AD,WA,WEA)" );
                        ps.Invoke( );
                    }
                }
                catch ( Exception )
                {
                    // locking is best-effort; the patch itself already applied
                }

                /*
                    launch StartAllBack
                */

                string path_sab_dir = Path.GetDirectoryName( path_dll );

                if ( !String.IsNullOrEmpty( path_sab_dir ) )
                {
                    string path_sab_exe = path_sab_dir + "\\" + Cfg.Default.app_target_exe;

                    if ( File.Exists( path_sab_exe ) )
                    {
                        System.Diagnostics.Process.Start( path_sab_exe );
                    }
                }
            }

            /*
                no DLL contained the signature: report instead of claiming success
            */

            if ( !patched_any )
                return Lng.statusbar_sab_not_found;

            /*
                bring the desktop back BEFORE showing the completion dialog,
                so the shell is already restored while the message box is up
            */

            RestoreShell( ourKey );
            shell_restored = true;

            MessageBox.Show(
                string.Format( Lng.msgbox_patch_compl_msg ),
                Lng.msgbox_patch_compl_title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );

            return Lng.statusbar_patch_complete;

            }
            finally
            {
                /*
                    safety net: on early return / exception the shell was not
                    restored above, so do it here. Always close the key.
                */

                if ( !shell_restored )
                    RestoreShell( ourKey );

                if ( ourKey != null )
                    ourKey.Close( );
            }
        }

        /*
            Restart the Windows shell (explorer) and re-enable Winlogon's
            automatic shell restart.
        */

        private static void RestoreShell( RegistryKey winlogonKey )
        {
            if ( winlogonKey != null )
                winlogonKey.SetValue( "AutoRestartShell", 1 );

            try { Process.Start( "explorer.exe" ); } catch { }
        }
    }
}
