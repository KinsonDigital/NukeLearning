# Feature Branches

## PR Status Checks

- [x] Check that all projects build
- [x] Check that all unit tests pass
- [x] Check that the destination branch is develop
- [x] Check that the source branch is a valid feature branch
- [ ] Check that the issue number in the branch is an issue that exists
  - Make sure to print a message to let the user know about the issue and that the branch needs to be recreated
- [ ] PR is linked to an issue
- [ ] PR title matches the title of the linked issue
- [ ] PR is assigned to a project
- [ ] Labels of the PR match the labels of the issue.  (The pr should always match the issue)
- [ ] The pr must have a reviewer
- [ ] The pr must be assigned



# Preview Feature Branches

## Status Checks

- [ ] Check that all projects build
- [ ] Check that all unit tests pass
- [ ] Check that the destination branch is a preview branch
- [ ] Check that verifies that branch syntax is correct
- [ ] Check that the issue number in the branch is an issue that exists



# Release Branches

## Status Checks

- [ ] Check that all projects build
- [ ] Check that all unit tests pass
- [ ] Versions are valid in csproj
- [ ] Release branch name is valid
- [ ] Validate 'v#.#.#' section of the release branch 
   - A version from csproj
- [ ] Check that a GitHub milestone has been created
- [ ] Check that a GitHub release does not already exist
- [ ] Check that a tag that matches the csproj version does not already exist
- [ ] Check that the release notes exist.  (Not preview release notes)
- [ ] Check that the release notes contain at least one issue number for each issue in the milestone
- [ ] Check that the destination branch is master for PRs
- [ ] Nuget package release does not exist yet


# Preview Release Branches

## Status Checks

- [ ] Check that all projects build
- [ ] Check that all unit tests pass
- [ ] Versions are valid in csproj
- [ ] Preview branch name is valid
- [ ] Validate 'v#.#.#-preview.#' section of the preview release branch 
   - A version from csproj
- [ ] Check that a GitHub milestone has been created
- [ ] Check that a GitHub release does not already exist
- [ ] Check that a tag that matches the csproj version does not already exist
- [ ] Check that the release notes exist.  (Not preview release notes)
- [ ] Check that the release notes contain at least one issue number for each issue in the milestone
- [ ] Check that the destination branch is a preview branch for PRs
- [ ] Nuget package release does not exist yet



# Preview Releases

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



# Production Releases

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