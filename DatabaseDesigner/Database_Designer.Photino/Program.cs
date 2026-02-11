using OpenSilver.Photino;
using Photino.NET;
using System.Diagnostics;
using System.Text;

namespace Database_Designer.Photino
{
    //NOTE: To hide the console window, go to the project properties and change the Output Type to Windows Application.
    // Or edit the .csproj file and change the <OutputType> tag from "WinExe" to "Exe".
    internal class Program
    {
        [STAThread]

        //Edits may be needed for Linux later
        static void Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            Console.BackgroundColor = ConsoleColor.Black;

            #region Coolart


            var WI =
        @"⠀                                     .----------.                                             
                                       .@@@@@@@@@@@@@@@@@@:                                         
                               =@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@-                                 
                            +@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@=                              
                          +*%@@@@@@@@@@@@@===       .-==%@@@@@@@@@@@@%*-                            
                       -*@@@@@@@@@@%*    .=@*****=====.      +@@@@@@@@@@@%:                         
                      *@@@@@@@@@@@*     -%            @@:      @@@@@@@@@@@@+                        
                   *@@@@@@@@+ -@@%    -@     .%.         @:     *@@+*@@@@@@@#                       
                 @@@@@@@@= =+ -*     -#  :@        @@@.    #.     #+*=    +@@@@@*                   
              *@@@@@@*-.  #* *%:   -@:  :#@         .--@:  -@-    -*@=    :-+@@@@@@+                
           .-%@@@@@#*-    #*+%=    -@   -%   .+@*@=     +==.@*:    -+:      :*#@@@@@@=              
          .@@@@@@@-      *@*+=     -@   -%.  .%.   @@:    %- #-     *=         .@@@@@@%.            
          %@@@@@-        *- +=     -@    :@   :@@   .%@.   #.#-     *@*          -@@@@@@.           
              @@@@@=     *- +=      .#     :@@      .%@.   #.#-     *@*       =@@@@#                
              -%@@@@@=   *- :##     .+#      .=#####@=.   *=.#-    +*#*     -@@@@@@:                
                =*%@@@@*=#-  =#*      :*-               -=*:=+:   #%%#= :=#@@@@@*=.                 
                   *@@@@@@@*. -*.       -@@.............#. .%-   .%+*==@@@@@@@*                     
                      *@@@@@@@# =%                       @@@.   *= +@@@@@@@+                        
                        +@@@@@@@@@@#               @@:        #@@@@@@@@@@#                          
                          :*@@@@@@@@@#########%@---     *####@@@@@@@@@#:                            
                             -*#@@@@@@@@@@@@@=======+@@@@@@@@@@@@@**:                               
                                  -@@@@@@@@@@@@@@@@@@@@@@@@@@@@.                                    
                                         :@@@@@@@@@@@@@@:                                           
                                                                                                    

 __       __            __  __                            ______              ______     __            __   ______    ______  
|  \  _  |  \          |  \|  \                          /      \            /      \   |  \          |  \ /      \  /      \ 
| $$ / \ | $$  ______  | $$| $$   __   ______    ______ |  $$$$$$\  ______  |  $$$$$$\ _| $$_         | $$|  $$$$$$\|  $$$$$$\
| $$/  $\| $$ |      \ | $$| $$  /  \ /      \  /      \| $$___\$$ /      \ | $$_  \$$|   $$ \         \$ | $$__/ $$| $$__/ $$
| $$  $$$\ $$  \$$$$$$\| $$| $$_/  $$|  $$$$$$\|  $$$$$$\\$$    \ |  $$$$$$\| $$ \     \$$$$$$             \$$    $$ \$$    $$
| $$ $$\$$\$$ /      $$| $$| $$   $$ | $$    $$| $$   \$$_\$$$$$$\| $$  | $$| $$$$      | $$ __            _\$$$$$$$ _\$$$$$$$
| $$$$  \$$$$|  $$$$$$$| $$| $$$$$$\ | $$$$$$$$| $$     |  \__| $$| $$__/ $$| $$        | $$|  \          |  \__/ $$|  \__/ $$
| $$$    \$$$ \$$    $$| $$| $$  \$$\ \$$     \| $$      \$$    $$ \$$    $$| $$         \$$  $$           \$$    $$ \$$    $$
 \$$      \$$  \$$$$$$$ \$$ \$$   \$$  \$$$$$$$ \$$       \$$$$$$   \$$$$$$  \$$          \$$$$             \$$$$$$   \$$$$$$ 
                                                                                                                              
                                                                                                                              
                                                                                                                              
   ____           ___             __  __         _____                ___   ____             ___       __        ___  __    ___     _______          
  / __/__ ___ ___/ (_)__  ___ _  / /_/ /  ___   / __(_)__ ____  ___ _/ (_) / __ \___  ___   / _ )__ __/ /____   / _ |/ /_  / _ |   /_  __(_)_ _  ___ 
 / _// -_) -_) _  / / _ \/ _ `/ / __/ _ \/ -_) _\ \/ / _ `/ _ \/ _ `/ /   / /_/ / _ \/ -_) / _  / // / __/ -_) / __ / __/ / __ |    / / / /  ' \/ -_)
/_/  \__/\__/\_,_/_/_//_/\_, /  \__/_//_/\__/ /___/_/\_, /_//_/\_,_/_( )  \____/_//_/\__/ /____/\_, /\__/\__/ /_/ |_\__/ /_/ |_|   /_/ /_/_/_/_/\__/ 
                        /___/                       /___/            |/                        /___/                                                 


";

            var ByWI =

    @"

┏┓ ╻ ╻   ╻ ╻┏━┓╻  ╻┏ ┏━╸┏━┓╺┳┓┏━╸╻ ╻   ┏━┓┏━╸   ╻ ╻┏━┓╻  ╻┏ ┏━╸┏━┓   ╻┏┓╻╺┳┓╻ ╻┏━┓╺┳╸┏━┓╻┏━╸┏━┓   ┏━┓┏┓╻╺┳┓
┣┻┓┗┳┛   ┃╻┃┣━┫┃  ┣┻┓┣╸ ┣┳┛ ┃┃┣╸ ┃┏┛   ┃ ┃┣╸    ┃╻┃┣━┫┃  ┣┻┓┣╸ ┣┳┛   ┃┃┗┫ ┃┃┃ ┃┗━┓ ┃ ┣┳┛┃┣╸ ┗━┓   ┣┳┛┃┗┫ ┃┃
┗━┛ ╹    ┗┻┛╹ ╹┗━╸╹ ╹┗━╸╹┗╸╺┻┛┗━╸┗┛    ┗━┛╹     ┗┻┛╹ ╹┗━╸╹ ╹┗━╸╹┗╸   ╹╹ ╹╺┻┛┗━┛┗━┛ ╹ ╹┗╸╹┗━╸┗━┛   ╹┗╸╹ ╹╺┻┛";


            var SumikaArt =


                 @"                                  :##=+#%#+.                                                                              
                               **.           =##:                                                                         
                            +*                   :#:                                                                      
                          #:                        #-                                                                    
                        +:                            #.                                                                  
                      +-                               =*                                                                 
                    -+                        .##*:      #                                                                
                   *                         ++=##=+#+    +.           -##+                                               
                 =-                        :*==*++*#===#+  *:       -#+++*=+*                                             
                :+                        *++**==+=+**===+#:*     **==+*##++=*+                                           
                 =:     -+:    +:       .*===+====+==+#+=+==## =#==+=#++==*=+==#.                                         
                  =:    -       *       *==*##*+++++++++++*#*%@***+++=====*==++=+*                                        
                   -=   =       .+    .%#++==++=+====+++++=++++======++*#+#+=++===*                                       
                     *           =.*#+=====+#++++========++===============+#+=+=++=#                                      
                      =-        ==   %++#%*+===+=====+=+++==============+==#+++==++=#.                                    
                        *    =*.    -+=+#+++=+==+==+=+%=+++====+++==+====+=++=+++====#                                    
                          +=       .*=+*+========+==*+=*=========++++====++++======+=+#                                   
                                   #=+*+==++======+#.  +==+=====+=#+=======+#=========+:                                  
                                  +++*=+++=+=+===%+..  :*+====+++#:-+===+=+=#+=====+==+%                                  
                                  %=*=+++==+*#*%=   ..  *++++=+=*-..-%##+++=%+=====++=+*-                                 
                                 ==+*=++#*#+++*.        .*==++=+=    :#==+#+#+======+=+=*                                 
                                 #=*++=+#*==*-           =++++=+      .#===+%++=========*:                                
                                -*=#+====#=#.             *==+#        .*=+=*%+=========+#                                
                                *+=#+=+*%*%               .#=*           #+=+*=++========*                                
                                #==#*#+=**#     .:-:.      -%:   ....     +++*===========+.                               
                               .*==++==+##* :#@@%#%#%@*     -  +@%##%@@@#- -%*===========+*                               
                               -+==+=+#: =:*%-                         :*@%.=*+===+=+++==+%                               
                               ++==+=#.  +                               .:=%*==+=++=++=+=%                               
                               *++=+=+= +-                                =*+*===+==++%==+%                               
                               #++*+*=*+*         +%*+***#**++--:.       ++=*+=++++==* *++#                               
                               #+   *==*=         +===============+    .*++=#++++==++   :%*                               
                                     %++=#=       ================+   +*+*#@@+++==*+                                      
                         .:=*###***+++#==*#@%-    ================+  *+.:+#*%===+#:......:--=*##*-.                       
                      .@-.............-**-....:=*=-+==============+--*#=#%=:#==+*................=*                       
                       -+..............++......=%%#**#%#*++++*%@@#*===+#%%.-+=*:................++                        
                        +=...................:%=------=#:#---=+=#=-------#=+*+.................#:                         
                         *.................%+#*------=%. #=.:#  *+==---==*+%+*................#                           
                          %-...............#.-##==-=-*-   % =-   %+=--=+*%*-=*..............=*                            
                           ++..............=+ . :##%####%%%@@%%%##%@@%+. ..-%..............#:                             
                            .%-.............#     :*+--=------==--=#+.     #:..........-=#*                               
                               =#%##+=:.....:@##=  *%%%%@@*=+=%@%%###    :%%....-+##=:                                    
                                .=++++#@%%=-#    :#@. -#=-=%#*=##  .@++%=  .@+##%@#*-:                                    
                              .%......*+-.-%*    :% :%=--#*  %===%- +@#    =%=..-#-...:#*                                 
                             -*.......%+ ::.-%+-%@- #*==+#   :%===## *=+#=%- :: -#+......*+                               
                            +-........-@%: -: -#%=   -%*@.    =#*%.   @#%: : .=%%..........*=                             
                           #.........#* .%%: :-*@-:.::-@@++++++%%::...:@@: .+%+ .#=.........:*                            
                         -*.........%:.**..##*#%.   .:             :   *##%#..=%#*#%..........#                           
                        =+..........:=........%.   #%*            %@:   -%.....................%.                         
                       =+...................=@.  =%==*            #=%:    %%....................*-                        
                      -%..................+@%   @+--=%            #==%     %@#...................=#                       
                      %..............:-*%+:%-:+#.....*=-----:::::-*  .%+----##+%%*=-...............%:                     
                     #+--=*##%%%%*=:     *%%%@%%%%@%%%%%%%%%%%%%%%%%@%%%#%@%@@+=======*@@****%%%#*-.                      
                                            +@%%%@#                 :@@@%@@  -%+=========+%#-                             
                                            #*++*%                   +*+++%.    #%+====+=====*%@+                         
                                           =%+++%=                   -%***#%.       =++#%%@@%#**+*%@@#:                   
                                        ==+*++=--::.  ..:.                                          .:-::                 ";





            #endregion

            string windowTitle = "Database_Designer";
            string apiExeName = "Pariah API.exe";
            string dbDesignerExeName = "Database Designer.exe";

            Console.Clear();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(WI);
            Task.Delay(2000).Wait();
            Console.WriteLine(ByWI);
            Task.Delay(2000).Wait();
            Console.WriteLine("Debug Console! Don't close this!");

            // 1. PRE-START CLEANUP
            // Kill existing instances before launching. 
            // If none exist, the helper method will simply log to console and continue.
            KillProcessesByName(apiExeName);
            KillProcessesByName(dbDesignerExeName);

            var exeDir = AppContext.BaseDirectory;

            // Creating a new PhotinoWindow instance
            var window = new PhotinoWindow()
                .SetTitle(windowTitle)
                .SetUseOsDefaultSize(true)
                .Center()
                .SetResizable(true)
                .SetLogVerbosity(0)
                .ConfigureOpenSilver<App>()
                .SetMaximized(true)
                .SetZoom(80)
                .SetDevToolsEnabled(false)
                .SetContextMenuEnabled(false);

            //NOW LAUNCH IT TAMASE MIKI, check mainpageCS line 732

           

            // 2. REGISTER CLOSING CLEANUP
            // This triggers when the user clicks 'X' or the app exits.
            window.WindowClosing += (sender, e) =>
            {
                Console.WriteLine("Closing detected. Cleaning up processes...");
                KillProcessesByName(apiExeName);
                // ExcludeCurrent ensures we don't kill the Photino window itself 
                // if it happens to share the same name as the designer.
                KillProcessesByName(dbDesignerExeName, excludeCurrent: true);
                return false; // Return false to allow the window to close
            };

            window.Load("wwwroot/index.html");
            window.WaitForClose();
            // --- Add F11 toggle for fullscreen ---
            window.RegisterWebMessageReceivedHandler((sender, message) =>
            {
                if (message == "toggle_fullscreen")
                {
                    window.SetFullScreen(!window.FullScreen);
                }
            });


            window.WaitForClose(); // Starts the application event loop
        }

        // Helper method used by the logic above
        public static void KillProcessesByName(string processName, bool excludeCurrent = false)
        {
            int currentId = Process.GetCurrentProcess().Id;
            string cleanName = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                               ? Path.GetFileNameWithoutExtension(processName)
                               : processName;

            var processes = Process.GetProcessesByName(cleanName);

            if (processes.Length == 0)
            {
                Console.WriteLine($"[CleanUp] No instances of {processName} found.");
                return;
            }

            foreach (var p in processes)
            {
                if (excludeCurrent && p.Id == currentId) continue;
                try
                {
                    p.Kill();
                    p.WaitForExit(1000);
                    Console.WriteLine($"[CleanUp] Successfully terminated {processName}.");
                }
                catch { /* Handle potential permission issues */ }
            }
        





        }
    }
}
