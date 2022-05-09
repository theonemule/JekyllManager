# Lightweight Manager for Jekyll v0.1

This project grew out of a desire to create a lightweight manager for Jekyll-based blogs. The idea was simple: use an Azure Function App to manage the Git repo that contains the site. The app makes a local copy of the repo relative to the function app, then allows a user to use a WYSIWYG editor for modifying drafts and posts in Jekyll. It also has a lightweight file manager to upload and download content to the repository, such as static assets. Changes are made locally relative to the editor and then pushed to the repo when the edits are finished.

To deploy, download the repository and deploy it to an Azure Function using the [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/functionapp/deployment?view=azure-cli-latest), [PowerShell](https://docs.microsoft.com/en-us/azure/azure-functions/create-first-function-cli-powershell?tabs=azure-cli%2Cbrowser), the [Function Tools](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local?tabs=v4%2Cwindows%2Ccsharp%2Cportal%2Cbash#publish), [VS Code](https://www.serverlessnotes.com/docs/deploy-azure-functions-with-visual-code), or [Visual Studio](https://docs.microsoft.com/en-us/azure/azure-functions/functions-develop-vs?tabs=in-process). Alternatively, fork this repo and deploy it to an Azure Function using a [GitHub Action](https://docs.microsoft.com/en-us/azure/azure-functions/functions-how-to-github-actions?tabs=dotnet).

An app on a Consumption Plan should be plenty enough to run this if you're not making changes. **Only Windows Function Apps** are supported because after I wrote this, I discovered that the native DLL that installs with the Nuget package for Linux does not seem to work with Azure Functions. If there's enough interest, I may change the backend to use octokit or some other library to make it work on Linux.

The app uses several settings:

* GITUSER: "youruser",
* GITEMAIL: "you@example.com",
* GITPAT: "A GitHub Persona Access Token",
* GITBRANCH: "yourbranch",
* GITREPO" "https://github.com/user/website.git"

These are configuration keys that can be set in the Auzre portal. You'll need to [obtain a GitHub Personal Access Token (PAT)](https://docs.github.com/en/enterprise-server@3.4/authentication/keeping-your-account-and-data-secure/creating-a-personal-access-token) from the GitHub portal to authenticate your account. The GITBRANCH is the branch you want to work from, and the GITREPO is the URL to the .git file used for cloning a repository.

Once the application is configured, point a browser to the URL for your Function app followed by /api/static to load the GUi. Ie. https://appname.azurewebsites.net/api/static

You'll need to obtain the Function App Key. This is used for authentication for the calls into the API's. You can obtain this from the Azure Portal by opening your Function App, selecting **"App Keys"** and copying the value next to _master or default. Default is sufficient. Paste the key into the text box for Function App Code, and select if you want to remember the code.

Once the UI loads, click **"Pull or Clone"** to get or update your local repository. This may take a second, depending on the size of your website repo.

**"Drafts"** loads the .md files in the _drafts folder. You can create a new draft, edit existing drafts, publish them to the posts folder, or delete a draft.

**"Posts"** contains .md files in the _posts folder. You can unpublished posts back to the _drafts folder, edit existing posts, and delete posts.

**"Everything"** is just a list of files in the repo. It allows you to edit most text-based files, upload files, download files, and delete files.

Once you have made changes, "Push Changes" will commit and push these changes to the repository. If there are Actions on the repository, these can be automatically deployed to the website.
