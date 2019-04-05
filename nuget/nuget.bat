nuget pack Transformalize.Provider.MySql.nuspec -OutputDirectory "c:\temp\modules"
nuget pack Transformalize.Provider.MySql.Autofac.nuspec -OutputDirectory "c:\temp\modules"

REM nuget push "c:\temp\modules\Transformalize.Provider.MySql.0.5.0-beta.nupkg" -source https://api.nuget.org/v3/index.json
REM nuget push "c:\temp\modules\Transformalize.Provider.MySql.Autofac.0.5.0-beta.nupkg" -source https://api.nuget.org/v3/index.json






