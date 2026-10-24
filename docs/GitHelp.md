# Git & GitHub Guide

A practical reference for working with Git and GitHub on shared projects.

---

## 1. Initial Setup

Do this once on your machine.

```bash
git config --global user.name "Your Name"
git config --global user.email "your@email.com"
```

Verify:

```bash
git config --list
```

---

## 2. Core Concepts

| Term                  | What it means                                                |
| --------------------- | ------------------------------------------------------------ |
| **Repository (repo)** | The project folder tracked by Git                            |
| **Commit**            | A saved snapshot of your changes                             |
| **Branch**            | An isolated line of work                                     |
| **Remote**            | The copy of the repo on GitHub                               |
| **Clone**             | Download a repo from GitHub to your machine                  |
| **Push**              | Upload your commits to GitHub                                |
| **Pull**              | Download changes from GitHub to your machine                 |
| **Merge**             | Combine changes from two branches                            |
| **Pull Request (PR)** | A request to merge your branch into another (done on GitHub) |

---

## 3. Getting a Repository

**Clone an existing repo:**

```bash
git clone https://github.com/org/repo-name.git
cd repo-name
```

---

## 4. Daily Workflow

This is the standard cycle you follow every working day.

### Step 1 — Sync with the latest code

Always do this before starting new work.

```bash
git checkout main
git pull origin main
```

### Step 2 — Create a branch for your work

Never work directly on `main`.

```bash
git checkout -b feature/your-feature-name
```

Branch naming convention:

```
feature/add-login-page
fix/null-pointer-on-submit
chore/update-dependencies
```

### Step 3 — Make changes, then stage and commit

Check what changed:

```bash
git status
git diff
```

Stage files:

```bash
git add file1.cs file2.cs   # specific files
git add .                   # all changed files
```

Commit with a clear message:

```bash
git commit -m "Add login form validation"
```

Good commit message rules:

- Use imperative mood: "Add", "Fix", "Remove", not "Added" or "Fixes"
- Keep the first line under 72 characters
- Describe _what_ and _why_, not _how_

### Step 4 — Push your branch to GitHub

```bash
git push origin feature/your-feature-name
```

### Step 5 — Open a Pull Request on GitHub

1. Go to the repository on GitHub
2. Click **"Compare & pull request"**
3. Fill in the title and description
4. Request a review from a teammate
5. Wait for approval before merging

---

## 5. Keeping Your Branch Up to Date

If `main` has moved forward while you were working on your branch:

```bash
git checkout main
git pull origin main
git checkout feature/your-feature-name
git merge main
```

Resolve any conflicts (see section 7), then commit.

---

## 6. Common Commands Reference

### Viewing Branches

```bash
# List local branches
git branch

# List all branches including remote
git branch -a

# List remote branches only
git branch -r

# Show last commit on each local branch
git branch -v

# Show which branches are already merged into current branch
git branch --merged

# Show branches NOT yet merged (safe to check before deleting)
git branch --no-merged
```

### Creating Branches

```bash
# Create a new branch and switch to it immediately
git checkout -b feature/branch-name

# Create a branch from a specific branch (not current)
git checkout -b feature/branch-name origin/main

# Create a branch without switching to it
git branch branch-name

# Create a local branch that tracks a remote branch
git checkout --track origin/branch-name
```

### Switching Branches

```bash
# Switch to an existing branch
git checkout branch-name

# Modern syntax (Git 2.23+)
git switch branch-name

# Switch back to the previous branch
git checkout -
git switch -
```

### Renaming Branches

```bash
# Rename the current branch
git branch -m new-name

# Rename a specific branch
git branch -m old-name new-name

# After renaming, update the remote
git push origin -u new-name
git push origin --delete old-name
```

### Deleting Branches

```bash
# Delete a local branch (safe — Git blocks if not merged)
git branch -d feature/branch-name

# Force delete a local branch (even if not merged)
git branch -D feature/branch-name

# Delete a remote branch on GitHub
git push origin --delete feature/branch-name

# Clean up local references to deleted remote branches
git fetch --prune
```

### Comparing Branches

```bash
# See commits in feature branch that are not in main
git log main..feature/branch-name --oneline

# See what files differ between two branches
git diff main..feature/branch-name --name-only

# See full diff between two branches
git diff main..feature/branch-name

# Check if a branch has been merged into main
git branch --merged main | grep branch-name
```

### Merging Branches

```bash
# Merge a branch into your current branch
git merge feature/branch-name

# Merge without fast-forward (preserves branch history in log)
git merge --no-ff feature/branch-name

# Abort a merge in progress
git merge --abort
```

### Rebasing (advanced — use with care)

Rebase replays your branch commits on top of another branch. It produces cleaner history than merge but rewrites commits — never rebase a branch others are using.

```bash
# Rebase current branch onto main
git rebase main

# Abort if conflicts get messy
git rebase --abort

# Continue after resolving a conflict
git rebase --continue
```

### Other Useful Branch Commands

```bash
# See commit history
git log --oneline

# Visualize branch graph in terminal
git log --oneline --graph --all

# Copy a single commit from another branch into current branch
git cherry-pick <commit-hash>

# Undo staged changes (before commit)
git restore --staged file.cs

# Undo local file changes (WARNING: irreversible)
git restore file.cs

# Stash work in progress temporarily
git stash
git stash pop        # restore latest stash
git stash list       # see all stashes
git stash drop       # discard latest stash

# See what's different from main
git diff main
```

---

## 7. Resolving Merge Conflicts

A conflict occurs when two branches changed the same lines.

Git marks conflicts like this inside the file:

```
<<<<<<< HEAD
your version of the code
=======
their version of the code
>>>>>>> main
```

Steps to resolve:

1. Open the conflicted file
2. Decide which version to keep (or combine both)
3. Delete the conflict markers (`<<<<<<<`, `=======`, `>>>>>>>`)
4. Stage the resolved file: `git add file.cs`
5. Commit: `git commit`

If it gets complicated, abort and start fresh:

```bash
git merge --abort
```

---

## 8. What NOT to Do

- **Do not force push to shared branches**: `git push --force` rewrites history and breaks others' work.
- **Do not commit directly to `main`**: Always use a branch and PR.
- **Do not commit secrets**: No passwords, API keys, or connection strings. Add them to `.gitignore`.
- **Do not use vague commit messages**: `"fix stuff"`, `"changes"`, `"aaa"` are unacceptable.
- **Do not commit unrelated changes in one commit**: Keep commits focused and atomic.

---

## 9. .gitignore

The `.gitignore` file tells Git which files to never track.

Example for a .NET project:

```
bin/
obj/
*.user
.vs/
appsettings.Development.json
```

If you accidentally committed something that should be ignored:

```bash
git rm --cached file-to-remove
git commit -m "Remove tracked file that should be ignored"
```

---

## 10. Quick Cheatsheet

```
Start of day:       git pull origin main
New task:           git checkout -b feature/name
Save progress:      git add . && git commit -m "message"
Upload work:        git push origin feature/name
Done with task:     Open PR on GitHub
```

---

## 11. Getting Help

```bash
git help <command>
git status        # always safe to run, shows current state
```

Online references:

- https://git-scm.com/docs
- https://docs.github.com
