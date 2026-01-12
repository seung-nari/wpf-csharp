1. 왜 이미지 타입을 먼저 구분해야 하는가

이미지 처리는 “다 같은 이미지 파일”이 아니다.

겉으로 보기엔 JPG, PNG, TIFF 모두 “이미지”지만,
 - 내부 구조
 - 메타데이터 방식
 - 지원 라이브러리
 - 처리 난이도
가 완전히 다르다.

특히 WPF에서 이미지를 다룰 때는
어떤 포맷인지 모르고 접근하면:

 - 메타데이터 조회 중 예외 발생
 - 특정 TIFF/GeoTIFF 파일이 열리지 않음
 - 좌표 정보(GeoTIFF)가 무시됨

그래서 코드 작성 전에 반드시 해야 할 일은:
    1. 이미지 포맷이 무엇인지 정의
    2. 그 포맷의 메타데이터 구조 이해
    3. 그에 맞는 라이브러리 선택

---------------------------------------------------------------------------

2. WPF / WIC 개념 정리
WPF (Windows Presentation Foundation)
 - Windows 데스크톱 UI 프레임워크
 - 버튼, 창, Image 컨트롤, XAML, 마우스 이벤트 등
 - 화면과 사용자 입력을 담당

WIC (Windows Imaging Component)
 - Windows에 기본 포함된 이미지 처리 엔진
 - 실제로 JPG/PNG/TIFF를 디코딩하는 주체
 - WPF의 BitmapImage, BitmapDecoder는 내부적으로 WIC를 사용

동작 구조:
    WPF(Image 컨트롤)
    → BitmapImage / BitmapDecoder
        → WIC(Windows 기본 코덱)
            → 이미지 파일

즉:
 - WPF는 “표시”
 - WIC는 “이미지 해석”

---------------------------------------------------------------------------

3. 이미지 포맷별 정의 (실무 기준)
3.1 JPG (JPEG)
 - 목적: 사진(손실 압축)
 - 특징:
     - 스마트폰/카메라 사진
     - EXIF 메타데이터가 잘 정의됨
 - WPF/WIC:
     - 표시 매우 안정적
     - EXIF 일부 조회 가능

3.2 PNG
 - 목적: 그래픽/로고/스크린샷/투명 이미지
 - 특징:
     - EXIF 개념 없음
     - 대신 텍스트 기반 메타데이터
         - tEXt
         - iTXt
         - zTXt

 - 주의점:
     - WPF의 meta.Title, meta.Author 같은
       프로퍼티 접근 시 예외 발생 가능
     - PNG는 “사진용 포맷”이 아님

3.3 TIFF (TIF)
 - 목적: 고품질 이미지, 스캔 문서, 멀티페이지
 - 특징:
     - 무손실
     - 페이지 여러 장 가능
     - 압축 방식 다양

 - WPF/WIC:
     - 기본 TIFF는 표시 가능
     - 일부 압축/BigTIFF/대용량에서 실패 가능

 - 메타데이터:
     - TIFF 태그 기반 구조

3.4 GeoTIFF
 - 정의:
     - TIFF + 지리 좌표 정보
 - 추가되는 정보:
     - 픽셀 → 지도 좌표 변환 정보
     - 좌표계(EPSG)
 - 핵심 태그:
     - ModelPixelScale (33550)
     - ModelTiepoint (33922)
     - ModelTransformation (34264)
     - GeoKeyDirectory (34735)
 - 특징:
     - 단순 “이미지 뷰어” 수준을 넘어서 GIS 영역

---------------------------------------------------------------------------

4. EXIF란 무엇인가
 - EXIF (Exchangeable Image File Format)
 - 주로 JPG 사진에 포함되는 촬영 정보

예:
 - 촬영일시
 - 카메라/폰 제조사, 모델
 - 방향(가로/세로)
 - GPS 위치(있는 경우)

PNG에는 EXIF가 없고,
TIFF/GeoTIFF는 EXIF와 다른 태그 구조를 사용한다.

---------------------------------------------------------------------------

5. PNG 메타데이터(tEXt / iTXt)와 WPF 예외 문제

PNG는 EXIF 대신 텍스트 청크 구조를 사용한다.

예:
Software = Photoshop
Comment  = Sample Image

하지만 WPF의 BitmapMetadata는:
 - 모든 이미지가
     - Title
     - Author
     - Subject
       같은 “공통 속성”을 가진다고 가정하고 프로퍼티를 제공한다.

그래서 PNG에서:
    meta.Title
    meta.Author

처럼 접근하면:
 - “이 코덱은 해당 속성을 지원하지 않습니다”
 - 예외 발생
    -> 이건 코드 실수가 아니라 포맷 구조 차이

---------------------------------------------------------------------------

6. GetQuery 기반 접근이란?
개념
 - 이미지 메타데이터는 트리 구조
 - GetQuery는 “경로 기반 조회”

예:
 - JPG EXIF:
    /app1/ifd/exif:{uint=36867}

 - PNG 텍스트:
    /tEXt/Software

왜 안전한가
 - 값이 없으면 null
 - try/catch로 방어 가능
 - 포맷별 구조 차이를 코드가 감당 가능

결론
 - ❌ meta.Title 같은 프로퍼티 접근
 - ⭕ meta.GetQuery("경로") 기반 접근

---------------------------------------------------------------------------

7. 라이브러리 선택 기준 (중요)
WPF / WIC (기본)

적합한 경우:
 - JPG/PNG/TIFF 단순 뷰어
 - 멀티페이지 TIFF 기본 처리
 - 간단한 메타데이터 표시

한계:
 - 특수 TIFF 압축
 - 대용량
 - GeoTIFF 좌표계 완전 해석 ❌

LibTiff.Net

적합한 경우:
 - TIFF 구조 자체를 정확히 다뤄야 할 때
 - 페이지/압축/태그 제어

한계:
 - GeoTIFF 좌표계 해석은 별도 구현 필요

GDAL (GeoTIFF 표준)

적합한 경우:
 - GeoTIFF 실데이터
 - 좌표계(EPSG) 필요
 - 회전/변환 포함 GeoTransform 처리
 - 현업 GIS 데이터

특징:
 - 네이티브 라이브러리
 - .NET에서는 C# 바인딩 사용
 - 설정이 조금 복잡하지만 가장 강력