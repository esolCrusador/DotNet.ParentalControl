taskkill /IM "DotNet.ParentalControl.exe" /F ^ 
rd /s /q "C:\Program Files\DotNet.ParentalControl" ^ 
cd DotNet.ParentalControl ^ 
dotnet publish -p:PublishProfile=FolderProfile ^ 
start /min "DotNet.ParentalControl" "C:\Program Files\DotNet.ParentalControl\DotNet.ParentalControl.exe"