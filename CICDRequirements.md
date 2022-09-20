# Feature Branches PR Status Check

- [x] Check that the source branch is a valid feature branch
- [x] Check that the target branch is a develop branch
- [x] Check that the issue number in the branch is an issue that exists
- [x] Check that the issue that is part of the branch contains a label
- [x] The PR must be assigned
- [x] Labels are added to the PR
- [ ] Project added to PR (Has to be done with regular GitHub API?)
  - Add issue for this later
- [x] Real world testing complete



# Preview Feature PR Status Check

- [x] Check that the source branch is a preview feature branch
- [x] Check that the target branch is a preview branch
- [x] Check that the issue number in the preview feature branch is an issue that exists
- [x] Check that the issue that is part of the branch contains a label
- [x] The PR must be assigned
- [x] Labels are added to the PR
- [ ] Project added to PR (Has to be done with regular GitHub API?)
  - Add issue for this later
- [x] Real world testing complete


# HotFix PR Status Check

- [ ] Check that the source branch is a hotfix branch
- [ ] Check that the target branch is a the master branch
- [ ] Check that the issue number in the hotfix branch is an issue that exists
- [ ] Check that the issue that is part of the branch contains a label
- [ ] The PR must be assigned
- [ ] Labels are added to the PR
- [ ] Project added to PR (Has to be done with regular GitHub API?)
  - Add issue for this later
- [ ] Real world testing complete


# Preview Release PR Status Check

- [x] Check for pull request only run
- [x] Versions are valid in csproj
- [x] Check that the source branch is a valid preview release branch
- [x] Check that the target branch is a release branch
- [x] Verify that the `v#.#.#` section of the preview branch (source) matches the `v#.#.#` section of the release branch (target)
- [x] Check that the PR has been assigned
- [x] Check that the PR has a `🚀Preview Release` label
- [x] Validate 'v#.#.#-preview.#' section of the preview branch (source) matches version in csproj
- [x] Check that a GitHub milestone has been created
- [x] Check that the milestone contains issues
- [x] Check that all of the milestone issues are closed
- [x] Check that the milestone has exactly one release todo issue item
- [x] Check that the milestone has exactly one release PR item
- [x] Check that all issues in a milestone have a label
- [x] Check that a tag that matches the csproj version does not already exist
- [x] Check that the release notes exist.  (Not prod release notes)
- [x] Check that the release notes contain at least one issue number for each issue in the milestone
- [x] Check that the release notes version in the title is correct
- [x] Check that a GitHub release does not already exist
- [x] Nuget package release does not exist yet
- [ ] Real world testing complete


# Production Release PR Status Check

- [x] Check for pull request only run
- [x] Versions are valid in csproj
- [x] Check that the source branch is a valid release branch
- [x] Check that the target branch is a master branch
- [x] Validate 'v#.#.#' section of the release branch matches version in csproj
- [x] Check that the PR has been assigned
- [x] Check that the PR has a `🚀Production Release` label
- [x] Check that a GitHub milestone has been created
- [x] Check that the milestone contains issues
- [x] Check that all of the milestone issues are closed
- [x] Check that the milestone has exactly one release todo issue item
- [x] Check that the milestone has exactly one release PR item
- [x] Check that all issues in a milestone have a label
- [x] Check that a tag that matches the csproj version does not already exist
- [x] Check that the release notes exist.  (Not prev release notes)
- [x] Check that the release notes contain at least one issue number for each issue in the milestone
- [x] Check that the release notes version in the title is correct
- [x] Check that a GitHub release does not already exist
- [x] Nuget package release does not exist yet
- [x] Real world testing complete



# Preview Release Workflow

## Checks
- [x] Check that it is not executed as a PR
- [x] Check that it is executed on a release branch
- [x] Versions are valid in csproj
- [x] Validate 'v#.#.#' section of the release branch matches version in csproj
- [x] Check that a tag that matches the csproj version does not already exist
- [x] Check that a GitHub milestone has been created
- [x] Check that the milestone contains issues
- [x] Check that all of the milestone issues are closed
- [x] Check that all of the milestone pull requests are closed
- [x] Check that the milestone has exactly one release todo issue item
- [x] Check that the milestone has exactly one release PR item
- [x] Check that all issues in a milestone have a label
- [x] Check that all pull requests in a milestone have a label
- [x] Check that the release notes exist.  (Not prev release notes)
- [x] Check that the release notes contain at least one issue number for each issue in the milestone
- [x] Check that the release notes version in the title is correct
- [x] GitHub release does not exist yet
- [x] Nuget package release does not exist yet
- [x] Builds execute - (TRIGGERS AFTER REQUIRES ALL PASS)
- [x] Unit tests execute - (TRIGGERS AFTER REQUIRES ALL PASS)
- [ ] Real world testing complete

## Deployment

- [x] Perform GitHub release
  - [x] Set correct title
  - [x] Set release notes
  - [x] Set correct tag
  - [x] Prints that release was successful with URL
- [x] Perform NuGet Release
  - [x] Creates package using Release config
  - [x] Prints that release was successful with URL
- [x] Close milestone
- [x] Send tweet announcement about release


# Production Release Workflow

- [x] Check that it is not executed as a PR
- [x] Check that it is executed on a master branch
- [x] Versions are valid in csproj
- [x] Check that a tag that matches the csproj version does not already exist
- [x] Check that a GitHub milestone has been created
- [x] Check that the milestone contains issues
- [x] Check that all of the milestone issues are closed
- [x] Check that all of the milestone pull requests are closed
- [x] Check that the milestone has exactly one release todo issue item
- [x] Check that the milestone has exactly one release PR item
- [x] Check that all issues in a milestone have a label
- [x] Check that all pull requests in a milestone have a label
- [x] Check that the release notes exist.  (Not prev release notes)
- [x] Check that the release notes contain at least one issue number for each issue in the milestone
- [x] Check that the release notes contain a preview release section if preview releases exist
- [x] Check that the release notes section contains a preview release item for each previous preview release if they exist
- [x] GitHub release does not exist yet
- [x] Nuget package release does not exist yet
- [x] Builds execute - (TRIGGERS AFTER REQUIRES ALL PASS)
- [x] Unit tests execute - (TRIGGERS AFTER REQUIRES ALL PASS)
- [ ] Real world testing complete

## Deployment

- [x] Perform GitHub release
  - [x] Set correct title
  - [x] Set release notes
  - [x] Set correct tag
  - [x] Prints that release was successful with URL
- [x] Perform NuGet Release
  - [x] Creates package using Release config
  - [x] Prints that release was successful with URL
- [ ] Merge master branch into develop branch
- [x] Close milestone
- [x] Send tweet announcement about release
