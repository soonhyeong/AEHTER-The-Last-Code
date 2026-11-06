# GitHub 업로드 안내

## 연결 저장소

[soonhyeong/AEHTER-The-Last-Code](https://github.com/soonhyeong/AEHTER-The-Last-Code)

이미 Git이 초기화되고 원격이 연결된 폴더에서는 아래 최초 생성 명령의 `git init`과 `git remote add`를 반복하지 않습니다. `git status`, `git remote -v`, `git branch -vv`로 현재 상태를 먼저 확인합니다.

## 업로드할 폴더

**AETHER-Portfolio 폴더의 내용만 업로드합니다.** 이 폴더의 부모인 원본 Unity 프로젝트를 저장소로 초기화하거나 통째로 끌어넣지 마세요.

현재 제출본에는 외부 바이너리 에셋과 실행 빌드가 없으므로, 준비된 파일 자체를 올리는 데 Git LFS 설정은 필요하지 않습니다. 이후 대용량 파일을 추가한다면 별도로 검토해야 합니다.

## 처음 새 저장소를 준비하는 경우

1. 팀 공동 코드의 공개 범위와 개인 기여도 표기 합의를 확인합니다.
2. GitHub에서 빈 저장소를 만듭니다. 기존 저장소가 없다면 README/.gitignore/LICENSE 자동 생성은 선택하지 않습니다. 이 폴더에 이미 준비되어 있습니다.
3. 공개 범위를 최종 확인하고 정상적인 GitHub 로그인/credential 절차로 인증합니다. 비밀번호나 token을 소스/문서에 적지 않습니다.
4. 아래 명령은 **이 폴더를 작업 디렉터리로 연 터미널에서만** 실행합니다. 사용자 계정/저장소 주소는 실제 값으로 바꿉니다.

```powershell
git init -b main
git add .
git status --short
git diff --cached --stat
git commit -m "Add AETHER code portfolio"
git remote add origin https://github.com/YOUR_ACCOUNT/YOUR_REPOSITORY.git
git push -u origin main
```

Git 작성자 정보가 설정되지 않았다면 commit 전에 해당 저장소에서 `git config user.name`과 `git config user.email`을 설정합니다. 공개 commit에 이메일이 노출되길 원하지 않으면 GitHub 계정에서 제공하는 비공개용 noreply 주소를 확인해 사용합니다.

이미 원격 저장소에 내용/이력이 있다면 위 새 저장소 절차를 그대로 실행하지 말고 먼저 기존 내용을 확인합니다. 임의 force push나 이력 덮어쓰기는 하지 않습니다.

## 연결 후 수정 내용을 반영하는 경우

```powershell
git status --short
git add .
git diff --cached --stat
git commit -m "Update AETHER portfolio"
git push
```

원격에 다른 작업자의 변경이 있다면 먼저 확인해 병합합니다. 충돌을 해결하려고 강제 푸시하거나 원격 파일을 임의 삭제하지 않습니다.

## 추후 추가할 자료

- 개인 담당 문서: 제출자가 작성한 실제 기여 내용을 별도 Markdown/PDF 등으로 추가합니다.
- 플레이 영상: 권리 확인된 실제 영상 링크를 README에 연결할 수 있습니다.
- 실행 빌드: 배포 권한과 실행 검증을 마친 후 별도 배포 링크나 Release 등으로 제공합니다. 에셋 원본 폴더를 빌드 대신 첨부하지 않습니다.
- 이미지: 현재 .gitignore는 실수로 외부 에셋을 넣지 않도록 이미지도 제외합니다. 직접 제작한 문서용 스크린샷을 추가할 때는 공개 권한을 확인하고 해당 경로에만 예외 규칙을 추가합니다.

이 파일은 절차 안내입니다. 실제 반영 상태는 GitHub 커밋 이력과 로컬 `git status`로 확인합니다. Git 작업은 이 제출 폴더에만 수행하고 원본 Unity 프로젝트는 저장소로 초기화하지 않습니다.
