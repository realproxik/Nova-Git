# NovaGit website

Open `index.html` in a browser for a static, responsive NovaGit product website. It intentionally uses an original Git-inspired interface rather than copying Git's branding or design.

## Deploy with GitHub Pages

The repository includes `.github/workflows/pages.yml`. Push the repository to GitHub, then open **Settings → Pages** and choose **GitHub Actions** as the publishing source. Every push to `main` or `master` that changes `website/` deploys the site.

GitHub Pages will use a URL in the form `https://YOUR-USERNAME.github.io/YOUR-REPOSITORY/`. A custom domain must be a fully qualified domain that you own; configure it through **Settings → Pages → Custom domain**, then add the DNS records provided by GitHub.
