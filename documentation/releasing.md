# Publishing a release

Pushing a version tag triggers GitHub Actions to build the self-contained
Windows x64 ZIP and attach it to a GitHub Release for that tag. From the main
checkout, tag a commit containing the release workflow, for example:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

Pushing the tag requires repository permission, and GitHub Actions must be
allowed to create releases with its `contents: write` token.
