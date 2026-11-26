# Code signing setup (SignPath Foundation)

Releases are signed for free by [SignPath Foundation](https://signpath.org). This is the one-time setup.
Until step 4 is done, the release workflow still works and publishes an unsigned exe.

1. **Apply** at <https://signpath.org/apply> with the GitHub repository URL. Turn on two-factor
   authentication for GitHub first: SignPath requires it for everyone on the project.
2. **After acceptance, in SignPath:**
   - Install the SignPath GitHub app (<https://github.com/apps/signpath>) on the RoboUI repository.
   - In the RoboUI project, link the trusted build system **GitHub.com**.
   - Add an artifact configuration with the contents of [artifact-configuration.xml](artifact-configuration.xml)
     and make it the default.
   - Use the signing policy slug `release-signing`, or set the repository variable
     `SIGNPATH_SIGNING_POLICY_SLUG` to the slug SignPath gives you.
     Likewise `SIGNPATH_PROJECT_SLUG` if the project slug isn't `RoboUI`.
   - Create an API token for a user with the submitter role on that policy.
3. **In GitHub** (repository Settings → Secrets and variables → Actions):
   - Variable `SIGNPATH_ORGANIZATION_ID`: the organization ID from SignPath.
4. **Last**, add the secret `SIGNPATH_API_TOKEN`. This turns signing on.

## Releasing with signing

Push a tag as usual. The workflow builds the exe, submits it to SignPath, and **waits up to two
hours for you to approve the signing request** in SignPath. The link to the request is in the
workflow log. After approval,
the signed exe is checked and attached to the GitHub release.
