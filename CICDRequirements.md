# Feature Branches PR Status Check

- [x] Check that the source branch is a valid feature branch
- [x] Check that the target branch is a develop branch
- [x] Check that the issue number in the branch is an issue that exists
- [x] The pr must be assigned
- [x] Labels are added to the PR
- [ ] Project added to PR (Has to be done with regular GitHub API?)
  - Add issue for this later
- [x] Real world testing complete



# Preview Feature PR Status Check

- [x] Check that the source branch is a preview feature branch
- [x] Check that the target branch is a preview branch
- [x] Check that the issue number in the preview feature branch is an issue that exists
- [x] The pr must be assigned
- [x] Labels are added to the PR
- [ ] Project added to PR (Has to be done with regular GitHub API?)
  - Add issue for this later
- [x] Real world testing complete


# Preview Release PR Status Check

- [ ] Versions are valid in csproj
- [x] Check that the source branch is a valid preview release branch
- [x] Check that the target branch is a release branch
- [ ] Verify that the `v#.#.#` section of the preview branch (source) matches the `v#.#.#` section of the release branch (target)
- [ ] Validate 'v#.#.#-preview.#' section of the preview branch (source) matches version in csproj
- [ ] Check that a GitHub milestone has been created
- [ ] Check that a GitHub release does not already exist
- [ ] Check that a tag that matches the csproj version does not already exist
- [ ] Check that the release notes exist.  (Not prod release notes)
- [ ] Check that the release notes contain at least one issue number for each issue in the milestone
- [ ] Nuget package release does not exist yet
- [ ] Real world testing complete


# Production Release PR Status Check

- [ ] Versions are valid in csproj
- [ ] Check that the source branch is a valid release branch
- [ ] Check that the target branch is a master branch
- [ ] Validate 'v#.#.#' section of the release branch matches version in csproj
- [ ] Check that a GitHub milestone has been created
- [ ] Check that a GitHub release does not already exist
- [ ] Check that a tag that matches the csproj version does not already exist
- [ ] Check that the release notes exist.  (Not prev release notes)
- [ ] Check that the release notes contain at least one issue number for each issue in the milestone
- [ ] Nuget package release does not exist yet
- [ ] Real world testing complete



# Preview Release Workflow

- [ ] Everything builds
- [ ] Unit tests pass
- [ ] Requires() to NOT be a PR
- [ ] Requires() on preview branch
- [ ] Versions in csproj is correct
- [ ] Release notes exist
- [ ] Check that the release notes contain at least one issue number for each issue in the milestone
- [ ] Nuget package release does not exist yet
- [ ] GitHub release does not exist yet
- [ ] Milestone exists
- [ ] Milestone state is validated
- [ ] Real world testing complete


# Production Release Workflow

- [ ] Everything builds
- [ ] Unit tests pass
- [ ] Requires() to NOT be a PR
- [ ] Requires() on master branch
- [ ] Versions in csproj is correct
- [ ] Release notes exist
- [ ] Check that the release notes contain at least one issue number for each issue in the milestone
- [ ] Nuget package release does not exist yet
- [ ] GitHub release does not exist yet
- [ ] Milestone exists
- [ ] Milestone state is validated
- [ ] Real world testing complete