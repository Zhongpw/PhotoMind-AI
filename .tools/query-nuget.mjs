// Lists versions matching a major.minor prefix for .NET 8 compatible pins.
const targets = [
  ['Microsoft.EntityFrameworkCore.Sqlite', '8.0.'],
  ['Microsoft.EntityFrameworkCore.Design', '8.0.'],
  ['Microsoft.Extensions.DependencyInjection', '8.0.'],
  ['Microsoft.Extensions.Hosting', '8.0.'],
  ['Microsoft.Extensions.Configuration.Json', '8.0.'],
  ['Serilog.Extensions.Logging', '8.0.'],
  ['Serilog.Sinks.File', '6.0.'],
  ['SixLabors.ImageSharp', '3.1.'],
  ['Microsoft.NET.Test.Sdk', '17.11.'],
  ['xunit.runner.visualstudio', '2.8.'],
  ['coverlet.collector', '6.0.'],
  ['Moq', '4.20.'],
];
for (const [p, prefix] of targets) {
  const url = `https://api.nuget.org/v3-flatcontainer/${p.toLowerCase()}/index.json`;
  try {
    const res = await fetch(url, { redirect: 'follow' });
    if (!res.ok) { console.log(`${p}: HTTP ${res.status}`); continue; }
    const data = await res.json();
    const versions = (Array.isArray(data) ? data : (data.versions || []))
      .filter((v) => !/[-+]/i.test(v) && v.startsWith(prefix));
    console.log(`${p} [${prefix}*] => ${versions.length ? versions[versions.length - 1] : '(none)'}`);
  } catch (e) {
    console.log(`${p}: ERROR ${e.message}`);
  }
}
