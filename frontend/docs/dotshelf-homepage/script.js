const modeContent = {
  room: {
    kicker: "Quarter View",
    title: "방 전체에서 내 서재를 한눈에 봅니다",
    description:
      "사용자는 따뜻한 3D 방에 들어와 책장, 노트북, 방명록 위치를 먼저 파악합니다. 첫 화면부터 dotshelf가 책 기반 공간 서비스라는 점이 드러납니다.",
    points: ["책장, 대시보드, 방명록을 같은 방 안에 배치", "PC와 태블릿에서 쿼터뷰 중심 탐색", "공간 오브젝트 클릭으로 기능 진입"],
  },
  shelf: {
    kicker: "Bookshelf View",
    title: "책장은 편집과 탐색이 분리된 핵심 공간입니다",
    description:
      "책장 화면에서는 슬롯 기반 배치, 중간 삽입, 이름 변경, 책장 추가와 삭제, 디자인 변경까지 한 흐름에서 다룹니다.",
    points: ["드래그 중에는 삽입 후보 슬롯을 시각적으로 표시", "편집 모드에서만 구조 변경 버튼 노출", "책장 회전과 벽면 연출을 함께 관리"],
  },
  book: {
    kicker: "Book Focus",
    title: "책을 누르면 읽기 기록으로 부드럽게 연결됩니다",
    description:
      "선택한 책은 현재 줌 정도에 맞춰 적절한 거리로 포커싱되고, 사용자는 독서 상태와 기록 패널을 바로 확인합니다.",
    points: ["책장이 가려지지 않는 포커싱 거리 유지", "상세 패널에서 책 정보와 기록 진입점 제공", "좋아요와 검색 같은 모니터 영역과 역할 분리"],
  },
  guestbook: {
    kicker: "Guestbook",
    title: "방명록은 서재 분위기를 해치지 않는 소셜 장치입니다",
    description:
      "방문자는 짧은 메시지를 남기고, 사용자는 책장을 중심으로 한 조용한 관계 흐름을 유지합니다.",
    points: ["코르크보드와 메모지 톤의 가벼운 표현", "짧은 메시지와 답글 중심의 상호작용", "피드보다 개인 공간에 가까운 방문 경험"],
  },
};

const tabs = document.querySelectorAll("[data-mode]");
const miniRoom = document.querySelector("[data-preview-mode]");
const kicker = document.querySelector("[data-mode-kicker]");
const title = document.querySelector("[data-mode-title]");
const description = document.querySelector("[data-mode-description]");
const points = document.querySelector("[data-mode-points]");

document.body.classList.add("js-ready");

function renderMode(mode) {
  const content = modeContent[mode];

  tabs.forEach((tab) => {
    const isActive = tab.dataset.mode === mode;
    tab.classList.toggle("is-active", isActive);
    tab.setAttribute("aria-selected", String(isActive));
  });

  miniRoom.dataset.previewMode = mode;
  kicker.textContent = content.kicker;
  title.textContent = content.title;
  description.textContent = content.description;
  points.replaceChildren(
    ...content.points.map((point) => {
      const item = document.createElement("li");
      item.textContent = point;
      return item;
    }),
  );
}

tabs.forEach((tab) => {
  tab.addEventListener("click", () => renderMode(tab.dataset.mode));
});

const revealTargets = document.querySelectorAll(
  ".intro-band, .studio-section, .experience-section, .showcase-section, .bookshelf-section, .flow-section, .mvp-section",
);

if ("IntersectionObserver" in window) {
  const observer = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.add("is-visible");
          observer.unobserve(entry.target);
        }
      });
    },
    { threshold: 0.12 },
  );

  revealTargets.forEach((target) => {
    target.classList.add("reveal");
    observer.observe(target);
  });
} else {
  revealTargets.forEach((target) => target.classList.add("is-visible"));
}

document.querySelectorAll('a[href^="#"]').forEach((anchor) => {
  anchor.addEventListener("click", (event) => {
    const target = document.querySelector(anchor.getAttribute("href"));

    if (!target) {
      return;
    }

    event.preventDefault();
    target.scrollIntoView({ behavior: "smooth", block: "start" });
  });
});
