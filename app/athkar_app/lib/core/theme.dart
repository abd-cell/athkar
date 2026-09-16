library;

import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

/// The design system, ported 1:1 from the «مخطوطة» direction of the design
/// prototype (`docs/DESIGN.md`).
///
/// The prototype is the authority on every value in this file. Where it used
/// `oklch()` — which Flutter has no equivalent for — the colour was converted
/// once, here, and the original is kept in a comment so the two can be checked
/// against each other later.
///
/// Screens never hardcode a colour. They read [AthkarTokens.of] and take what
/// they need; that is what makes the light and dark palettes a single swap
/// instead of a conditional in every widget.

/// Brand constants that do not change with brightness.
class AthkarColors {
  const AthkarColors._();

  /// The deep green of the design. `oklch(0.42 0.07 150)`.
  static const brand = Color(0xFF2F5838);

  /// The darker shade used for text on a tinted ground. `oklch(0.38 0.07 150)`.
  static const brandInk = Color(0xFF244D2E);

  /// The lighter green the dark palette uses, so it keeps its contrast against
  /// near-black. `oklch(0.62 0.09 150)`.
  static const brandDark = Color(0xFF5D9669);

  /// Text weight of that green on a dark ground. `oklch(0.78 0.09 150)`.
  static const brandDarkInk = Color(0xFF8EC899);

  /// The colour of the device frame in the prototype; also the dark status bar.
  static const chrome = Color(0xFF23201C);
}

/// Every colour, radius and text style a screen is allowed to use.
///
/// A `ThemeExtension` rather than a set of global constants, because the two
/// palettes are the same *shape*: a screen asks for `tokens.surface` and gets
/// parchment or near-black without knowing which it is in.
@immutable
class AthkarTokens extends ThemeExtension<AthkarTokens> {
  const AthkarTokens({
    required this.brightness,
    required this.paper,
    required this.readingPaper,
    required this.surface,
    required this.ink,
    required this.muted,
    required this.faint,
    required this.hairline,
    required this.border,
    required this.brand,
    required this.brandInk,
    required this.brandTint,
    required this.onBrand,
    required this.scrimTop,
    required this.alert,
    required this.alertTint,
    required this.warning,
    required this.warningTint,
    required this.success,
    required this.successTint,
    required this.info,
    required this.infoTint,
  });

  final Brightness brightness;

  /// The page ground. Parchment in light, near-black in dark.
  final Color paper;

  /// A shade deeper than [paper], for the uninterrupted reading view — the
  /// prototype darkens the reading screen so long passages sit quieter.
  final Color readingPaper;

  /// Cards and raised surfaces.
  final Color surface;

  /// Primary text.
  final Color ink;

  /// Secondary text: labels, counts, attributions.
  final Color muted;

  /// Tertiary text, one step quieter than [muted].
  final Color faint;

  /// Dividers inside a card.
  final Color hairline;

  /// Outlines of cards, chips and outlined buttons.
  final Color border;

  final Color brand;

  /// Brand at text weight, for a label sitting on [brandTint].
  final Color brandInk;

  /// The brand at low opacity, for the next-prayer strip and similar.
  final Color brandTint;

  /// What sits on top of a solid [brand] fill.
  final Color onBrand;

  /// The translucent ground behind a bottom bar that floats over content.
  final Color scrimTop;

  /// The four voices an alert can speak in. A dialog or a toast asks for the
  /// pair — [alert] for the glyph and the label, [alertTint] for the wash it
  /// sits on — so neither is ever a literal in a screen.
  ///
  /// They are parchment colours, not Material ones: a terracotta rather than
  /// #F44336, because a saturated red on this ground reads as a different app.
  final Color alert;
  final Color alertTint;
  final Color warning;
  final Color warningTint;
  final Color success;
  final Color successTint;
  final Color info;
  final Color infoTint;

  static const _lightHairline = Color(0x1F241D16); // rgba(36,29,22,.12)
  static const _lightBorder = Color(0x29241D16); // rgba(36,29,22,.16)
  static const _darkHairline = Color(0x1FECE4D6); // rgba(236,228,214,.12)
  static const _darkBorder = Color(0x2EECE4D6); // rgba(236,228,214,.18)

  static const light = AthkarTokens(
    brightness: Brightness.light,
    paper: Color(0xFFF4ECE0),
    readingPaper: Color(0xFFF4ECE0),
    surface: Color(0xFFFBF6EE),
    ink: Color(0xFF241D16),
    muted: Color(0xFF6F6153),
    faint: Color(0xFF8C8171),
    hairline: _lightHairline,
    border: _lightBorder,
    brand: AthkarColors.brand,
    brandInk: AthkarColors.brandInk,
    brandTint: Color(0x172F5838), // brand at 9%
    onBrand: Colors.white,
    scrimTop: Color(0xF0F4ECE0),
    alert: Color(0xFF9B3A3A),
    alertTint: Color(0x149B3A3A),
    warning: Color(0xFF8A6520),
    warningTint: Color(0x148A6520),
    success: Color(0xFF3F6B45),
    successTint: Color(0x143F6B45),
    info: Color(0xFF3A5C77),
    infoTint: Color(0x143A5C77),
  );

  static const dark = AthkarTokens(
    brightness: Brightness.dark,
    paper: Color(0xFF1B1814),
    readingPaper: Color(0xFF15130F),
    surface: Color(0xFF232019),
    ink: Color(0xFFECE4D6),
    muted: Color(0xFF9A8F7F),
    faint: Color(0xFF6F6558),
    hairline: _darkHairline,
    border: _darkBorder,
    brand: AthkarColors.brandDark,
    brandInk: AthkarColors.brandDarkInk,
    brandTint: Color(0x245D9669), // brand at 14%
    onBrand: Color(0xFF14120F),
    scrimTop: Color(0xF21B1814),
    alert: Color(0xFFD98C86),
    alertTint: Color(0x24D98C86),
    warning: Color(0xFFD9B168),
    warningTint: Color(0x24D9B168),
    success: Color(0xFF7FB98A),
    successTint: Color(0x247FB98A),
    info: Color(0xFF8FB6D4),
    infoTint: Color(0x248FB6D4),
  );

  static AthkarTokens of(BuildContext context) =>
      Theme.of(context).extension<AthkarTokens>() ?? light;

  bool get isDark => brightness == Brightness.dark;

  @override
  AthkarTokens copyWith({
    Brightness? brightness,
    Color? paper,
    Color? readingPaper,
    Color? surface,
    Color? ink,
    Color? muted,
    Color? faint,
    Color? hairline,
    Color? border,
    Color? brand,
    Color? brandInk,
    Color? brandTint,
    Color? onBrand,
    Color? scrimTop,
    Color? alert,
    Color? alertTint,
    Color? warning,
    Color? warningTint,
    Color? success,
    Color? successTint,
    Color? info,
    Color? infoTint,
  }) {
    return AthkarTokens(
      brightness: brightness ?? this.brightness,
      paper: paper ?? this.paper,
      readingPaper: readingPaper ?? this.readingPaper,
      surface: surface ?? this.surface,
      ink: ink ?? this.ink,
      muted: muted ?? this.muted,
      faint: faint ?? this.faint,
      hairline: hairline ?? this.hairline,
      border: border ?? this.border,
      brand: brand ?? this.brand,
      brandInk: brandInk ?? this.brandInk,
      brandTint: brandTint ?? this.brandTint,
      onBrand: onBrand ?? this.onBrand,
      scrimTop: scrimTop ?? this.scrimTop,
      alert: alert ?? this.alert,
      alertTint: alertTint ?? this.alertTint,
      warning: warning ?? this.warning,
      warningTint: warningTint ?? this.warningTint,
      success: success ?? this.success,
      successTint: successTint ?? this.successTint,
      info: info ?? this.info,
      infoTint: infoTint ?? this.infoTint,
    );
  }

  @override
  AthkarTokens lerp(covariant AthkarTokens? other, double t) {
    if (other == null) return this;
    return AthkarTokens(
      brightness: t < 0.5 ? brightness : other.brightness,
      paper: Color.lerp(paper, other.paper, t)!,
      readingPaper: Color.lerp(readingPaper, other.readingPaper, t)!,
      surface: Color.lerp(surface, other.surface, t)!,
      ink: Color.lerp(ink, other.ink, t)!,
      muted: Color.lerp(muted, other.muted, t)!,
      faint: Color.lerp(faint, other.faint, t)!,
      hairline: Color.lerp(hairline, other.hairline, t)!,
      border: Color.lerp(border, other.border, t)!,
      brand: Color.lerp(brand, other.brand, t)!,
      brandInk: Color.lerp(brandInk, other.brandInk, t)!,
      brandTint: Color.lerp(brandTint, other.brandTint, t)!,
      onBrand: Color.lerp(onBrand, other.onBrand, t)!,
      scrimTop: Color.lerp(scrimTop, other.scrimTop, t)!,
      alert: Color.lerp(alert, other.alert, t)!,
      alertTint: Color.lerp(alertTint, other.alertTint, t)!,
      warning: Color.lerp(warning, other.warning, t)!,
      warningTint: Color.lerp(warningTint, other.warningTint, t)!,
      success: Color.lerp(success, other.success, t)!,
      successTint: Color.lerp(successTint, other.successTint, t)!,
      info: Color.lerp(info, other.info, t)!,
      infoTint: Color.lerp(infoTint, other.infoTint, t)!,
    );
  }
}

/// The measurements the prototype repeats often enough to be worth naming.
class AthkarSpacing {
  const AthkarSpacing._();

  /// The horizontal margin every screen's content sits inside.
  static const page = 24.0;

  static const cardRadius = 18.0;
  static const smallCardRadius = 14.0;
  static const buttonRadius = 12.0;

  /// Anything tappable is at least this tall. The prototype sets it on every
  /// control, and it is the one accessibility number worth being dogmatic about.
  static const tapTarget = 44.0;
}

/// Type, in the two families the design uses.
///
/// Amiri carries anything narrated — a dhikr, a hadith, a heading — because it
/// is the face the text was set in for centuries. IBM Plex Sans Arabic carries
/// the interface around it: labels, counts, buttons. Mixing them is the whole
/// character of the direction, so the two are named rather than left to
/// individual widgets to remember.
class AthkarType {
  const AthkarType._();

  /// The scripture and heading face.
  static TextStyle amiri({
    required double size,
    required Color color,
    FontWeight weight = FontWeight.w400,
    double? height,
  }) =>
      GoogleFonts.amiri(
        fontSize: size,
        color: color,
        fontWeight: weight,
        height: height,
      );

  /// The interface face.
  static TextStyle sans({
    required double size,
    required Color color,
    FontWeight weight = FontWeight.w400,
    double? height,
    double? letterSpacing,
  }) =>
      GoogleFonts.ibmPlexSansArabic(
        fontSize: size,
        color: color,
        fontWeight: weight,
        height: height,
        letterSpacing: letterSpacing,
      );
}

/// Builds the two [ThemeData]s from the tokens above.
class AthkarTheme {
  const AthkarTheme._();

  static ThemeData light() => _build(AthkarTokens.light);

  static ThemeData dark() => _build(AthkarTokens.dark);

  static ThemeData _build(AthkarTokens tokens) {
    final scheme = ColorScheme.fromSeed(
      seedColor: tokens.brand,
      brightness: tokens.brightness,
    ).copyWith(
      primary: tokens.brand,
      onPrimary: tokens.onBrand,
      surface: tokens.surface,
      onSurface: tokens.ink,
    );

    return ThemeData(
      useMaterial3: true,
      brightness: tokens.brightness,
      colorScheme: scheme,
      scaffoldBackgroundColor: tokens.paper,
      // The design has no elevation anywhere: cards are outlined parchment, not
      // floating paper, and a Material shadow under one breaks the whole look.
      cardTheme: CardThemeData(
        color: tokens.surface,
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AthkarSpacing.cardRadius),
          side: BorderSide(color: tokens.border),
        ),
      ),
      dividerTheme: DividerThemeData(color: tokens.hairline, thickness: 1, space: 1),
      appBarTheme: AppBarTheme(
        backgroundColor: tokens.paper,
        foregroundColor: tokens.ink,
        elevation: 0,
        scrolledUnderElevation: 0,
        centerTitle: true,
        titleTextStyle: AthkarType.amiri(
          size: 20,
          color: tokens.ink,
          weight: FontWeight.w700,
        ),
      ),
      textTheme: _textTheme(tokens),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: tokens.brand,
          foregroundColor: tokens.onBrand,
          minimumSize: const Size(0, AthkarSpacing.tapTarget),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AthkarSpacing.buttonRadius),
          ),
          textStyle: AthkarType.sans(size: 13, color: tokens.onBrand, weight: FontWeight.w600),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: tokens.ink,
          minimumSize: const Size(0, AthkarSpacing.tapTarget),
          side: BorderSide(color: tokens.border),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AthkarSpacing.buttonRadius),
          ),
          textStyle: AthkarType.sans(size: 12.5, color: tokens.ink, weight: FontWeight.w500),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: tokens.brand,
          textStyle: AthkarType.sans(size: 12.5, color: tokens.brand, weight: FontWeight.w500),
        ),
      ),
      switchTheme: SwitchThemeData(
        thumbColor: WidgetStateProperty.resolveWith(
          (states) => states.contains(WidgetState.selected) ? tokens.onBrand : tokens.surface,
        ),
        trackColor: WidgetStateProperty.resolveWith(
          (states) => states.contains(WidgetState.selected) ? tokens.brand : tokens.border,
        ),
      ),
      sliderTheme: SliderThemeData(
        activeTrackColor: tokens.brand,
        inactiveTrackColor: tokens.border,
        thumbColor: tokens.brand,
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: tokens.surface,
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AthkarSpacing.smallCardRadius),
          borderSide: BorderSide(color: tokens.border),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AthkarSpacing.smallCardRadius),
          borderSide: BorderSide(color: tokens.border),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AthkarSpacing.smallCardRadius),
          borderSide: BorderSide(color: tokens.brand),
        ),
        hintStyle: AthkarType.sans(size: 13, color: tokens.faint),
      ),
      bottomSheetTheme: BottomSheetThemeData(
        backgroundColor: tokens.paper,
        surfaceTintColor: Colors.transparent,
        shape: const RoundedRectangleBorder(
          borderRadius: BorderRadius.vertical(top: Radius.circular(26)),
        ),
      ),
      extensions: [tokens],
    );
  }

  static TextTheme _textTheme(AthkarTokens tokens) => TextTheme(
        // Display and headline are Amiri: they carry names of chapters and the
        // greeting, both of which read as part of the text rather than the chrome.
        displaySmall: AthkarType.amiri(
            size: 27, color: tokens.ink, weight: FontWeight.w700, height: 1.3),
        headlineSmall: AthkarType.amiri(size: 22, color: tokens.ink, weight: FontWeight.w700),
        titleLarge: AthkarType.amiri(size: 17, color: tokens.ink, weight: FontWeight.w700),
        titleMedium: AthkarType.sans(size: 13.5, color: tokens.ink, weight: FontWeight.w500),
        bodyLarge: AthkarType.amiri(size: 21, color: tokens.ink, height: 1.95),
        bodyMedium: AthkarType.sans(size: 13, color: tokens.ink),
        bodySmall: AthkarType.sans(size: 11.5, color: tokens.muted),
        labelLarge: AthkarType.sans(size: 12.5, color: tokens.ink, weight: FontWeight.w500),
        labelSmall: AthkarType.sans(size: 11, color: tokens.muted),
      );
}
