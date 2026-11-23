// Prints the SHA512 hash (from official release metadata) for a given file name.
// Matches by URL basename, since the `name` field is not exactly the download filename.
const name = process.argv[2] || 'dotnet-sdk-8.0.428-win-x64.zip';
const res = await fetch('https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json', { redirect: 'follow' });
const j = await res.json();
for (const rel of j.releases) {
  const sets = [...(rel.sdks || []), ...(rel.runtimes || []), ...(rel.aspnetcoreRuntimes || [])];
  for (const set of sets) {
    for (const f of set.files || []) {
      const url = f.url || '';
      if (url.endsWith('/' + name)) {
        console.log(url);
        console.log(f.hash || '(no hash)');
        process.exit(0);
      }
    }
  }
}
console.error('not found: ' + name);
process.exit(1);
