# windows
dotnet publish -c Release -r win-x64 -p:PublishTrimmed=true -p:PublishSingleFile=true

# linux
# dotnet publish -c Release -r linux-x64 -p:PublishTrimmed=true -p:PublishSingleFile=true