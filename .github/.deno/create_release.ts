import * as semver from "https://deno.land/std@0.194.0/semver/mod.ts";

const last_tag = Deno.env.get("LAST_TAG");
const owner = Deno.env.get("OWNER");
const repo = Deno.env.get("REPO");
const token = Deno.env.get("GITHUB_TOKEN");

// 作成されるであろうリリースノートを取得
const dataMayRelease = {
    owner: owner,
    repo: repo,
    tag_name: "v999.999.999",
    target_commitish: "main",
    previous_tag_name: last_tag,
};

const responseMayRelease = await fetch(`https://api.github.com/repos/${owner}/${repo}/releases/generate-notes`, {
    method: "POST",
    headers: {
        "Accept": "application/vnd.github+json",
        "Authorization": `Bearer ${token}`,
        "X-GitHub-Api-Version": "2022-11-28",
    },
    body: JSON.stringify(dataMayRelease),
},);

const mayReleaseJson = await responseMayRelease.json();
const mayRelease = mayReleaseJson.body;

let increment = "";
const arrayMajor = ["### Removed",];
const arrayMinor = ["### Added", "### Changed", "### Deprecated",];
const arrayPatch = ["### Fixed", "### Security"];
if (arrayMajor.some(v => mayRelease.includes(v))) {
    increment = "major";
} else if (arrayMinor.some(v => mayRelease.includes(v))) {
    increment = "minor";
} else {
    increment = "patch";
}

const current = semver.parse(last_tag);
const next = semver.format(semver.increment(current, increment));

const dataTrueRelease = {
    owner: owner,
    repo: repo,
    tag_name: `v${next}`,
    target_commitish: "main",
    previous_tag_name: last_tag,
};

const responseTrueRelease = await fetch(`https://api.github.com/repos/${owner}/${repo}/releases/generate-notes`, {
    method: "POST",
    headers: {
        "Accept": "application/vnd.github+json",
        "Authorization": `Bearer ${token}`,
        "X-GitHub-Api-Version": "2022-11-28",
    },
    body: JSON.stringify(dataTrueRelease),
},);

// もう一度リリースノートを作成してCHANGELOGに追記
const trueReleaseJson = await responseTrueRelease.json();

// 現在のリリースノートをすべて取得
let currentReleases = [];
currentReleases.push(`## ${trueReleaseJson.name}`);
currentReleases.push(trueReleaseJson.body.replace("<!-- Release notes generated using configuration in .github/release.yml at main -->", ""));
let pages = 0;
const per = 100;
while (true) {
    pages++;
    const data = {
        per_page: per,
        page: pages,
    };
    const responseCurrentRelease = await fetch(`https://api.github.com/repos/${owner}/${repo}/releases?${new URLSearchParams(data)}`, {
        method: "GET",
        headers: {
            "Accept": "application/vnd.github+json",
            "Authorization": `Bearer ${token}`,
            "X-GitHub-Api-Version": "2022-11-28",
        },
    },);
    const parsed = await responseCurrentRelease.json();
    parsed.filter(r => !r.draft).forEach((v) => {
        currentReleases.push(`## ${v.name}`);
        currentReleases.push(v.body.replace("<!-- Release notes generated using configuration in .github/release.yml at main -->", ""));
    });
    if (parsed.length < per) {
        break;
    }
}

Deno.writeTextFileSync("CHANGELOG.md", currentReleases.join("\n"));

const packageJsonRaw = await Deno.readTextFile("./Packages/EasyHttpRPC/package.json");
const packageJson = JSON.parse(packageJsonRaw);
packageJson.version = next;
await Deno.writeTextFile("./Packages/EasyHttpRPC/package.json", JSON.stringify(packageJson, null, 2) + "\n");

console.log(`v${next}`);
