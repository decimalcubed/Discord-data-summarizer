# Discord data summarizer
Really low quality mid script I wrote really quickly, it reads a discord data packets messages and gives you really basic information.

Gives you information on how many messages you posted, and your most chatted in channels, for the years 2020-2025 (its hardcoded im too lazy to do it better)
Doesnt give ANY information on how much time youve spent in vcs, because the file i would need to parse for that is like 4 million lines long and im NOT doing all that

THIS PROGRAM IS REALLY JANKY and not very polished i wrote it very quickly and sloppily, please read the below instructions if you do not know what you are doing

If you are on MacOS/Linux, you will have to compile this yourself

# To use:
First, obtain a data package, if you already have one, skip this
1. Go to 'Data & Privacy' in settings.
2. Scroll down to 'Request your data'
3. Request a package with the 'Messages' option enabled
4. Wait to recieve the package in the email
5. Download and unzip it


Download/Compile the data summarizer 
- You can do this by navigating to the 'Releases' section on this page
- To compile, clone this repository, cd into this folder and run `dotnet build`


Then, with your data package
1. Open DiscordDataSummarizer-win64.exe
2. Copy+Paste the directory/path of the unzipped data package into it
3. Press enter
4. Press enter again (or Y then enter if you want some debug info)
5. Input number of top channels to display (or press enter for default)
6. Wait a bit
   
the program SHOULD now display info about your message count + most used channels for the years 2020-2025, if it doesnt PLEASE make an issue here
you might have to scroll up a bit or resize the window to see all of it
