# AutoGitChangelogGenerator

Auto git chenge-log generator.

Usage:
Parameter 1: Repository path
Parameter 2: Start recording log time, generally the time when the last tag was created on the same branch
Parameter 3 (optional): End recording log time, generally the time when the CI is triggered, default value is Now

Format for time points: year-month-day-hour-minute-second

Example: dotnet AutoChangelog.dll C:/Programs/Project 2024-5-29-8-15-0
