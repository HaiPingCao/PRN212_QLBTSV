# Git rules

- **Never commit on your own.** Do not run `git commit` (or `git commit --amend`, `git merge`, `git rebase`, `git cherry-pick`, `git stash` or anything else that creates or rewrites commits) unless the user explicitly asks for it in the current message.
- **Never push.** Do not run `git push` unless the user explicitly asks for it in the current message. Permission given for one push doesn't carry over to the next one.
- Leave changes uncommitted in the working tree. At the end of a task, list what changed so the user can review it and commit it themselves.
- Read-only git commands (`status`, `diff`, `log`, `branch`, `show`) are always fine.
- Never force push, never rewrite published history, and never change git config.
